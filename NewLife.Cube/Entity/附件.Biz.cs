﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;

namespace NewLife.Cube.Entity
{
    /// <summary>附件。用于记录各系统模块使用的文件</summary>
    public partial class Attachment : Entity<Attachment>
    {
        #region 对象操作
        static Attachment()
        {
            // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
            //var df = Meta.Factory.AdditionalFields;
            //df.Add(nameof(Size));

            // 过滤器 UserModule、TimeModule、IPModule
            Meta.Modules.Add<UserModule>();
            Meta.Modules.Add<TimeModule>();
            Meta.Modules.Add<IPModule>();
        }

        /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
        /// <param name="isNew">是否插入</param>
        public override void Valid(Boolean isNew)
        {
            // 如果没有脏数据，则不需要进行任何处理
            if (!HasDirty) return;

            // 这里验证参数范围，建议抛出参数异常，指定参数名，前端用户界面可以捕获参数异常并聚焦到对应的参数输入框
            if (FileName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(FileName), "文件名不能为空！");

            // 在新插入数据或者修改了指定字段时进行修正
            // 处理当前已登录用户信息，可以由UserModule过滤器代劳
            /*var user = ManageProvider.User;
            if (user != null)
            {
                if (isNew && !Dirtys[nameof(CreateUserID)]) CreateUserID = user.ID;
                if (!Dirtys[nameof(UpdateUserID)]) UpdateUserID = user.ID;
            }*/
            //if (isNew && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
            //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
            //if (isNew && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
            //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;
        }

        ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
        //[EditorBrowsable(EditorBrowsableState.Never)]
        //protected override void InitData()
        //{
        //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
        //    if (Meta.Session.Count > 0) return;

        //    if (XTrace.Debug) XTrace.WriteLine("开始初始化Attachment[附件]数据……");

        //    var entity = new Attachment();
        //    entity.ID = 0;
        //    entity.Category = "abc";
        //    entity.FileName = "abc";
        //    entity.Size = 0;
        //    entity.ContentType = "abc";
        //    entity.Path = "abc";
        //    entity.Hash = "abc";
        //    entity.CreateUser = "abc";
        //    entity.CreateUserID = 0;
        //    entity.CreateIP = "abc";
        //    entity.CreateTime = DateTime.Now;
        //    entity.UpdateUser = "abc";
        //    entity.UpdateUserID = 0;
        //    entity.UpdateIP = "abc";
        //    entity.UpdateTime = DateTime.Now;
        //    entity.Remark = "abc";
        //    entity.Insert();

        //    if (XTrace.Debug) XTrace.WriteLine("完成初始化Attachment[附件]数据！");
        //}

        ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
        ///// <returns></returns>
        //public override Int32 Insert()
        //{
        //    return base.Insert();
        //}

        ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
        ///// <returns></returns>
        //protected override Int32 OnDelete()
        //{
        //    return base.OnDelete();
        //}
        #endregion

        #region 扩展属性
        #endregion

        #region 扩展查询
        /// <summary>根据编号查找</summary>
        /// <param name="id">编号</param>
        /// <returns>实体对象</returns>
        public static Attachment FindByID(Int32 id)
        {
            if (id <= 0) return null;

            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.ID == id);

            // 单对象缓存
            return Meta.SingleCache[id];

            //return Find(_.ID == id);
        }

        /// <summary>根据分类查找</summary>
        /// <param name="category">分类</param>
        /// <returns>实体列表</returns>
        public static IList<Attachment> FindAllByCategory(String category)
        {
            // 实体缓存
            if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Category.EqualIgnoreCase(category));

            return FindAll(_.Category == category);
        }
        #endregion

        #region 高级查询
        /// <summary>高级查询</summary>
        /// <param name="category">分类</param>
        /// <param name="key">关键字</param>
        /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
        /// <returns>实体列表</returns>
        public static IList<Attachment> Search(String category, String key, PageParameter page)
        {
            var exp = new WhereExpression();

            if (!category.IsNullOrEmpty()) exp &= _.Category == category;
            if (!key.IsNullOrEmpty()) exp &= _.FileName == key | _.ContentType.Contains(key) | _.Path.StartsWith(key) | _.Title.Contains(key);

            return FindAll(exp, page);
        }

        // Select Count(ID) as ID,Category From Attachment Where CreateTime>'2020-01-24 00:00:00' Group By Category Order By ID Desc limit 20
        static readonly FieldCache<Attachment> _CategoryCache = new FieldCache<Attachment>(nameof(Category))
        {
            //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
        };

        /// <summary>获取分类列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
        /// <returns></returns>
        public static IDictionary<String, String> GetCategoryList() => _CategoryCache.FindAllName();
        #endregion

        #region 业务操作
        #endregion
    }
}