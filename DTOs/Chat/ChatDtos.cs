namespace TaskControlBackend.DTOs.Chat;

public record CreateOneToOneRequest(Guid UserId);
public record CreateGroupRequest(string Name, List<Guid> MemberIds);
public record SendMessageRequest(string Text);
public record AddGroupMemberRequest(Guid UserId);
