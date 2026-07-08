namespace Shared.Infrastructure.Security;

public static class AuthConstants
{
    #region Token
    public const string ClaimUserId = "U";
    public const string ClaimSourceChannel = "C";

    public const string ChannelFrontEnd = "1";
    public const string ChannelExternalApi = "2";
    public const string ChannelMobileApp = "3";
    #endregion
}