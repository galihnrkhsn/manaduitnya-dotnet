using ManaDuitnya.Api.Dtos;
using ManaDuitnya.Api.Entities;
using ManaDuitnya.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using ManaDuitnya.Api.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace ManaDuitnya.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public class TransactionController(TransactionRepository repo) : ControllerBase
{
    private long UserId => User.GetUserId();

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateTime from, [FromQuery] DateTime to) => Ok(await repo.ListAsync(UserId, from, to));

    [HttpPost]
    public async Task<IActionResult> Create(CreateTransactionRequest req)
    {
        if (req.Type == TransactionType.Transfer && req.ToWalletId is null)
            return BadRequest("Transfer harus punya dompet tujuan.");

        var t = new Transaction
        {
            Uuid            = Guid.NewGuid(),
            UserId          = UserId,
            WalletId        = req.WalletId,
            ToWalletId      = req.ToWalletId,
            CategoryId      = req.CategoryId,
            Type            = req.Type,
            Amount          = req.Amount,
            Note            = req.Note,
            TransactionDate = req.TransactionDate
        };

        await repo.InsertAsync(t);
        return Created($"api/transactions/{t.Uuid}", t);
    }
}