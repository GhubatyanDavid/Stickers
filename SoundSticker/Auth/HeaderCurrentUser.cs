using SoundSticker.Contracts;

namespace SoundSticker.Auth;

public sealed class HeaderCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public const string UserIdHeaderName = "X-User-Id";

    public string UserId
    {
        get
        {
            var context = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HTTP context is unavailable.");
            var userId = context.Request.Headers[UserIdHeaderName].ToString().Trim();
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new MissingUserIdException();
            }

            return userId;
        }
    }
}

public sealed class MissingUserIdException()
    : InvalidOperationException($"Missing required {HeaderCurrentUser.UserIdHeaderName} header.");
