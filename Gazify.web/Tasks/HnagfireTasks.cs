using Microsoft.AspNetCore.Identity.UI.Services;

namespace Gazify.Tasks
{
    public class HnagfireTasks
    {
        private readonly ApplicationDbContext _context;

        private readonly IEmailBodyBuilder _emailBodyBuilder;
        private readonly IEmailSender _emailSender;

        public HnagfireTasks(ApplicationDbContext context, IEmailBodyBuilder emailBodyBuilder, IEmailSender emailSender)
        {
            _context = context;
            _emailBodyBuilder = emailBodyBuilder;
            _emailSender = emailSender;
        }
        public async Task PrepareExpirationAlert()
        {
            var subscribers = _context.Subscribers
                .Include(s => s.Subscriptions)
                .Where(s => !s.IsBlackListed && s.Subscriptions.OrderByDescending(x => x.EndDate).First().EndDate == DateTime.Today.AddDays(5))
                .ToList();

            foreach (var subscriber in subscribers)
            {
                var endDate = subscriber.Subscriptions.Last().EndDate.ToString("d MMM, yyyy");

                //Send email and WhatsApp Message
                var placeholders = new Dictionary<string, string>()
                {
                    { "imageUrl", "" },
                    { "header", $"Hello {subscriber.FirstName}," },
                    { "body", $"your subscription will be expired by {endDate}" }
                };

                var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Notification, placeholders);

                await _emailSender.SendEmailAsync(
                    subscriber.Email,
                    "Gazify Subscription Expiration", body);

            }
        }

    }
}