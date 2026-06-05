using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Services;

public class InternalEmailService
{
    private readonly IEmailProviderAdapter _emailAdapter;
    private readonly ILogger<InternalEmailService> _logger;

    public InternalEmailService(IEmailProviderAdapter emailAdapter, ILogger<InternalEmailService> logger)
    {
        _emailAdapter = emailAdapter;
        _logger = logger;
    }

    public async Task<InternalSendEmailResultDto> SendAsync(InternalSendEmailDto request)
    {
        try
        {
            var result = await _emailAdapter.SendAsync(new EmailSendPayload
            {
                To = request.To,
                From = request.From,
                Subject = request.Subject,
                Body = request.Body,
                Html = request.Html,
                ReplyTo = request.ReplyTo
            });

            if (result.Success)
            {
                _logger.LogInformation("Internal email sent to {To}", request.To);
                return new InternalSendEmailResultDto { Success = true };
            }

            _logger.LogWarning("Internal email failed to {To}: {Error}", request.To, result.Failure?.Message);
            return new InternalSendEmailResultDto { Success = false, Error = result.Failure?.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal email error sending to {To}", request.To);
            return new InternalSendEmailResultDto { Success = false, Error = ex.Message };
        }
    }
}
