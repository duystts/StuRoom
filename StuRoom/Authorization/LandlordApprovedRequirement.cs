using Microsoft.AspNetCore.Authorization;

namespace StuRoom.Authorization;

/// <summary>
/// Yêu cầu Landlord phải được Admin duyệt (IsApproved = true) mới được truy cập.
/// </summary>
public class LandlordApprovedRequirement : IAuthorizationRequirement { }
