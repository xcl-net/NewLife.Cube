﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NewLife.Cube.Web.Models;
using NewLife.Log;
using NewLife.Remoting;
using XCode.Membership;

namespace NewLife.Web.OAuth
{
    /// <summary>企业微信身份验证提供者</summary>
    public class QyWeiXin : OAuthClient
    {
        #region 属性
        /// <summary>企业Ids</summary>
        public String CorpId { get => Key; set => Key = value; }

        /// <summary>应用凭证</summary>
        public String CorpSecret { get => Secret; set => Secret = value; }

        ///// <summary>访问令牌</summary>
        //public String AccessToken { get; set; }

        ///// <summary>过期时间</summary>
        //public DateTime Expire { get; set; }

        ///// <summary>性能跟踪</summary>
        //public ITracer Tracer { get; set; } = DefaultTracer.Instance;

        //private HttpClient _client;
        private String _uri = "https://qyapi.weixin.qq.com/";
        #endregion

        #region 构造
        /// <summary>实例化</summary>
        public QyWeiXin()
        {
            Server = "https://open.weixin.qq.com/connect/oauth2/";

            AuthUrl = "authorize?response_type={response_type}&appid={key}&redirect_uri={redirect}&state={state}&scope={scope}#wechat_redirect";

            var qyapi = "https://qyapi.weixin.qq.com/cgi-bin/";
            AccessUrl = qyapi + "gettoken?corpid={key}&corpsecret={secret}";
            UserUrl = qyapi + "user/getuserinfo?access_token={token}&code={code}";

            Scope = "snsapi_base";
        }
        #endregion

        #region 令牌
        /// <summary>获取令牌</summary>
        /// <returns></returns>
        public async Task<String> GetToken()
        {
            if (CorpId.IsNullOrEmpty()) throw new ArgumentNullException(nameof(CorpId));
            if (CorpSecret.IsNullOrEmpty()) throw new ArgumentNullException(nameof(CorpSecret));

            //var url = $"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={CorpId}&corpsecret={CorpSecret}";

            var client = GetClient();
            //_client = Tracer.CreateHttpClient();
            //_client.BaseAddress = new Uri("https://qyapi.weixin.qq.com");
            var rs = await client.GetAsync<IDictionary<String, Object>>(_uri + "cgi-bin/gettoken", new { corpid = CorpId, corpsecret = CorpSecret });

            AccessToken = rs["access_token"] as String;
            var exp = rs["expires_in"].ToInt(-1);
            if (exp > 0) Expire = DateTime.Now.AddSeconds(exp);

            return AccessToken;
        }

        /// <summary>获取访问令牌</summary>
        /// <returns></returns>
        protected async Task<String> GetAccessToken()
        {
            if (!String.IsNullOrEmpty(AccessToken) && Expire > DateTime.Now.AddMinutes(1)) return AccessToken;

            return await GetToken();
        }
        #endregion

        #region 通信录
        /// <summary>获取部门列表</summary>
        /// <returns></returns>
        public async Task<DepartmentInfo[]> GetDepartments()
        {
            var token = await GetAccessToken();

            var client = GetClient();
            var rs = await client.InvokeAsync<DepartmentInfo[]>(HttpMethod.Get, _uri + "cgi-bin/department/list", new { access_token = token }, null, "department");

            return rs;
        }

        /// <summary>获取用户</summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<UserInfo> GetUser(String userId)
        {
            var token = await GetAccessToken();

            var client = GetClient();
            var rs = await client.InvokeAsync<UserInfo>(HttpMethod.Get, _uri + "cgi-bin/user/get", new
            {
                userid = userId,
                access_token = token
            }, null, "");

            return rs;
        }

        /// <summary>获取部门内用户列表</summary>
        /// <param name="departmentId"></param>
        /// <param name="fetchChild"></param>
        /// <returns></returns>
        public async Task<UserInfo[]> GetUsers(Int32 departmentId, Boolean fetchChild = false)
        {
            var token = await GetAccessToken();

            var client = GetClient();
            var rs = await client.InvokeAsync<UserInfo[]>(HttpMethod.Get, _uri + "cgi-bin/user/list", new
            {
                department_id = departmentId,
                fetch_child = fetchChild ? 1 : 0,
                access_token = token
            }, null, "userlist");

            return rs;
        }
        #endregion

        #region 考勤
        /// <summary>获取考勤数据</summary>
        /// <param name="userIds"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public async Task<CheckInData[]> GetCheckIn(String[] userIds, DateTime start, DateTime end)
        {
            var token = await GetAccessToken();

            var client = GetClient();
            var rs = await client.InvokeAsync<CheckInData[]>(HttpMethod.Post, _uri + "cgi-bin/checkin/getcheckindata?access_token=" + token, new
            {
                opencheckindatatype = 3,
                starttime = start.ToInt(),
                endtime = end.ToInt(),
                useridlist = userIds,
            }, null, "checkindata");

            return rs;
        }
        #endregion

        #region 审批
        /// <summary>获取审批数据</summary>
        /// <param name="templateId"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="cursor"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public async Task<String[]> GetApprovals(String templateId, DateTime start, DateTime end, Int32 cursor = 0, Int32 size = 100)
        {
            var token = await GetAccessToken();

            var client = GetClient();
            var rs = await client.InvokeAsync<String[]>(HttpMethod.Post, _uri + "cgi-bin/oa/getapprovalinfo?access_token=" + token, new
            {
                starttime = start.ToInt(),
                endtime = end.ToInt(),
                cursor,
                size,
                filters = new[] {
                    new { key = "template_id", value = templateId },
                    new { key = "sp_status", value = "2" },
                },
            }, null, "sp_no_list");

            return rs;
        }

        /// <summary>获取审批单详情</summary>
        /// <param name="sp_no"></param>
        /// <returns></returns>
        public async Task<ApprovalInfo> GetApproval(String sp_no)
        {
            var token = await GetAccessToken();

            var client = GetClient();
            var rs = await client.InvokeAsync<ApprovalInfo>(HttpMethod.Post, _uri + "cgi-bin/oa/getapprovaldetail?access_token=" + token, new
            {
                sp_no
            }, null, "info");

            return rs;
        }
        #endregion

        #region 辅助
        /// <summary>获取配置</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static String GetConfig(String name) => Parameter.GetOrAdd(0, "QyWeiXin", name)?.Value;

        /// <summary>依据魔方字典参数的配置，创建企业微信对象</summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static QyWeiXin CreateFor(String name)
        {
            return new QyWeiXin
            {
                CorpId = QyWeiXin.GetConfig("CorpId"),
                CorpSecret = QyWeiXin.GetConfig(name),
            };
        }
        #endregion

        #region 用户信息增强
        /// <summary>企业内部应用获取用户信息</summary>
        /// <returns></returns>
        public override String GetUserInfo()
        {
            var html = base.GetUserInfo();

            // 获取用户详情
            if (!UserName.IsNullOrEmpty())
            {
                var user = Task.Run(() => GetUser(UserName)).Result;
                if (user != null)
                {
                    NickName = user.Name;
                    Mobile = user.Mobile;
                    Mail = user.Mail;
                    Avatar = user.Avatar;
                    Detail = user.Alias;
                }
            }

            return html;
        }

        /// <summary>从响应数据中获取信息</summary>
        /// <param name="dic"></param>
        protected override void OnGetInfo(IDictionary<String, String> dic)
        {
            base.OnGetInfo(dic);

            if (dic.TryGetValue("UserId", out var str)) UserName = str.Trim();
            if (dic.TryGetValue("DeviceId", out str)) DeviceId = str.Trim();
        }
        #endregion
    }
}