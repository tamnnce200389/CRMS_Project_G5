namespace CRMS.Services;

public interface IUserSession { long? UserId { get; } string Role { get; } long SessionVersion { get; } }
public readonly record struct SessionStamp(long UserId, long Version)
{
    public static SessionStamp Capture(IUserSession session) => session.UserId is > 0 ? new(session.UserId.Value, session.SessionVersion) : throw new InvalidOperationException("Vui lòng đăng nhập.");
    public void Ensure(IUserSession session)
    {
        if (session.UserId != UserId || session.SessionVersion != Version) throw new InvalidOperationException("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
    }
}
