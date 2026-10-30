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
        return Ok(await repo.GetAll(UserId));
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

        await repo.Create(wallet);

        return CreatedAtAction(nameof(GetById), new { id = wallet.Id }, wallet);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var wallet = await repo.GetById(id, UserId);
        return wallet is null ? NotFound() : Ok(wallet);
    }

    [HttpGet("archived")]
    public async Task<IActionResult> GetArchived()
    {
        return Ok(await repo.GetArchived());
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Archive(long id)
    {
        var archived = await repo.Archive(id, UserId);
        return archived 
            ? Ok(new { message = "Dompet berhasil dihapus." })
            : NotFound(new { message = "Dompet tidak ditemukan!" });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, WalletDtos req)
    {
        req.Name = req.Name.Trim().ToUpper();
        var updated = await repo.Update(id, UserId, req);
        return updated
            ? Ok(new { message = "Dompet berhasil diubah." })
            : NotFound(new { message = "Dompet tidak ditemukan!" });
    }
}