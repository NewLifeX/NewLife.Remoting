using System;

namespace NewLife.IoT.Models
{
    /// <summary>设备登录响应</summary>
    public class LoginResponse
    {
        #region 属性
        /// <summary>产品</summary>
        public String ProductKey { get; set; }

        /// <summary>节点编码</summary>
        public String Code { get; set; }

        /// <summary>节点密钥</summary>
        public String Secret { get; set; }

        /// <summary>名称</summary>
        public String Name { get; set; }

        /// <summary>令牌</summary>
        public String Token { get; set; }

        /// <summary>服务器时间</summary>
        public Int64 Time { get; set; }

        ///// <summary>设备通道</summary>
        //public String Channels { get; set; }

        /// <summary>客户端唯一标识</summary>
        public String ClientId { get; set; }
        #endregion
    }
}