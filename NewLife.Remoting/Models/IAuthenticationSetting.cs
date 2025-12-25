namespace NewLife.Remoting.Models;

/// <summary>身份验证设置。用于IPasswordProvider</summary>
public interface IAuthenticationSetting
{
    /// <summary>身份验证哈希算法。支持md5/sha1/sha512，默认md5</summary>
    String Algorithm { get; set; }

    /// <summary>身份验证盐值时间。使用Unix秒作为盐值，该值为允许的最大时间差。0表示不使用时间盐值，而是使用随机字符串。默认60秒</summary>
    Int32 SaltTime { get; set; }
}
