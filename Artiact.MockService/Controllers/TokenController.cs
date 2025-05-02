using Artiact.Contracts.Models.Api;
using Microsoft.AspNetCore.Mvc;

namespace Artiact.SmartProxy.Controllers;

[ApiController]
[Route( "token" )]
public class TokenController
{
    [HttpPost]
    public ActionResult<TokenContainer> Token()
    {
        return new TokenContainer
        {
            Token = "dsdsad"
        };
    }
}