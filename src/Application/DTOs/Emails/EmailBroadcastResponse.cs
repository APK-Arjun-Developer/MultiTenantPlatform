namespace Application.DTOs.Emails;

public class EmailBroadcastResponse
{
    public int RecipientCount { get; set; }
    public string Message { get; set; } = default!;
}
