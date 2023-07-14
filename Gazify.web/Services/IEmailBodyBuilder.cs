namespace Gazify.Services
{
    public interface IEmailBodyBuilder
    {
        string GetEmailBody(string template, Dictionary<string, string> placeholders);
    }
}
