using ManaDuitnya.Api.Dtos;
using ManaDuitnya.Api.Entities;
using ManaDuitnya.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using ManaDuitnya.Api.Extensions;
using Microsoft.AspNetCore.Authorization;


namespace ManaDuitnya.Api.Controllers;
[ApiController]
[Authorize]
[Route("api/wallets")]
public class WalletController(WalletRepository repo) : ControllerBase
{
    private long UserId => User.GetUserId();
    [HttpGet]
    public async Task<IActionResult> List()
    {
        return Ok(await repo.ListAsync(UserId));
    }
    
    [HttpPost]
    public async Task<IActionResult> CreateWallet(WalletDtos req) 
    {
        var wallet = new Wallet
        {
            UserId = UserId,
            Name = req.Name.Trim(),
            InitialBalance = req.InitialBalance
        };

        await repo.InsertAsync(wallet);

        return CreatedAtAction(nameof(GetById), new { id = wallet.Id }, wallet);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var wallet = await repo.GetByIdAsync(id, UserId);
        return wallet is null ? NotFound() : Ok(wallet);
    }

    [HttpGet("archived")]
    public async Task<IActionResult> GetArchived()
    {
        return Ok(await repo.GetArchivedAsync());
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Archive(long id)
    {
        var archived = await repo.ArchiveAsync(id, UserId);
        return archived 
            ? Ok(new { message = "Dompet berhasil dihapus." })
            : NotFound(new { message = "Dompet tidak ditemukan!" });
    }
}