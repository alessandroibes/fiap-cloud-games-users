using FiapCloudGames.Users.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FiapCloudGames.Users.Api;

public sealed record RegisterRequest(string Name, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record RoleRequest(string Role);

[ApiController, Route("api/users")]
public sealed class UsersController(UserService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<UserView>> Register(RegisterRequest request, CancellationToken ct) =>
        StatusCode(201, await service.Register(request.Name, request.Email, request.Password, ct));
}

[ApiController, Route("api/auth")]
public sealed class AuthController(UserService service) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
        Ok(new { accessToken = await service.Login(request.Email, request.Password, ct) });
}

[ApiController, Route("api/admin/users"), Authorize(Roles = "Admin")]
public sealed class AdminUsersController(UserService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.List(ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.Get(id, ct));
    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, RoleRequest request, CancellationToken ct)
    {
        await service.ChangeRole(id, request.Role, ct);
        return NoContent();
    }
}
