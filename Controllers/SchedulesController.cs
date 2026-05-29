using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServicioRESTEjecucionComandos.DTOs;
using ServicioRESTEjecucionComandos.Models;
using ServicioRESTEjecucionComandos.Repositories;

namespace ServicioRESTEjecucionComandos.Controllers;

/// <summary>
/// Controller providing CRUD endpoints for ETL schedule management.
/// Requires authentication via JWT bearer token.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly EtlScheduleRepository _scheduleRepo;
    private readonly ILogger<SchedulesController> _logger;

    /// <summary>
    /// Initializes a new instance of SchedulesController.
    /// </summary>
    public SchedulesController(
        EtlScheduleRepository scheduleRepo,
        ILogger<SchedulesController> logger)
    {
        _scheduleRepo = scheduleRepo;
        _logger = logger;
    }

    /// <summary>
    /// Gets all ETL schedules.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllSchedules()
    {
        var schedules = await _scheduleRepo.GetAllAsync();
        var dtos = schedules.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Gets a single ETL schedule by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetScheduleById(Guid id)
    {
        var schedule = await _scheduleRepo.GetByIdAsync(id);
        if (schedule == null)
        {
            return NotFound(new { error = $"Schedule with Id {id} not found." });
        }

        return Ok(MapToDto(schedule));
    }

    /// <summary>
    /// Creates a new ETL schedule.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateEtlScheduleDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var schedule = new EtlSchedule
        {
            CodEnvio = dto.CodEnvio,
            TipoEntidad = dto.TipoEntidad,
            Codigo = dto.Codigo,
            CronExpression = dto.CronExpression,
            IsActive = true
        };

        await _scheduleRepo.CreateAsync(schedule);

        _logger.LogInformation("Created new schedule {ScheduleId} for code {Codigo}.", schedule.Id, schedule.Codigo);

        return CreatedAtAction(nameof(GetScheduleById), new { id = schedule.Id }, MapToDto(schedule));
    }

    /// <summary>
    /// Updates an existing ETL schedule.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSchedule(Guid id, [FromBody] UpdateEtlScheduleDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var schedule = await _scheduleRepo.GetByIdAsync(id);
        if (schedule == null)
        {
            return NotFound(new { error = $"Schedule with Id {id} not found." });
        }

        schedule.CodEnvio = dto.CodEnvio;
        schedule.TipoEntidad = dto.TipoEntidad;
        schedule.Codigo = dto.Codigo;
        schedule.CronExpression = dto.CronExpression;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _scheduleRepo.UpdateAsync(schedule);

        _logger.LogInformation("Updated schedule {ScheduleId}.", id);

        return Ok(MapToDto(schedule));
    }

    /// <summary>
    /// Toggles the active state of an ETL schedule.
    /// </summary>
    [HttpPatch("{id}/toggle")]
    public async Task<IActionResult> ToggleScheduleActive(Guid id)
    {
        var schedule = await _scheduleRepo.GetByIdAsync(id);
        if (schedule == null)
        {
            return NotFound(new { error = $"Schedule with Id {id} not found." });
        }

        await _scheduleRepo.ToggleActiveAsync(id);

        // Reload to get updated values
        schedule = await _scheduleRepo.GetByIdAsync(id);
        _logger.LogInformation("Toggled schedule {ScheduleId} to IsActive={IsActive}.", id, schedule?.IsActive);

        return Ok(MapToDto(schedule!));
    }

    /// <summary>
    /// Deletes an ETL schedule.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSchedule(Guid id)
    {
        var schedule = await _scheduleRepo.GetByIdAsync(id);
        if (schedule == null)
        {
            return NotFound(new { error = $"Schedule with Id {id} not found." });
        }

        await _scheduleRepo.DeleteAsync(id);

        _logger.LogInformation("Deleted schedule {ScheduleId}.", id);

        return NoContent();
    }

    /// <summary>
    /// Maps an EtlSchedule model to an EtlScheduleDto response.
    /// </summary>
    private static EtlScheduleDto MapToDto(EtlSchedule schedule)
    {
        return new EtlScheduleDto
        {
            Id = schedule.Id,
            CodEnvio = schedule.CodEnvio,
            TipoEntidad = schedule.TipoEntidad,
            Codigo = schedule.Codigo,
            CronExpression = schedule.CronExpression,
            IsActive = schedule.IsActive,
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };
    }
}
