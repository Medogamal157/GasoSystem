using Gazify.Filters;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.UI.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Gazify.Controllers
{
    [Authorize(Roles = AppRoles.Reception)]
    public class SubscribersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDataProtector _dataProtector;
        private readonly IMapper _mapper;
        private readonly IEmailSender _emailSender;
        private readonly IEmailBodyBuilder _emailBodyBuilder;

        private readonly IWebHostEnvironment _webHostEnvironment;
        private List<string> _allowedExtensions = new() { ".jpg", ".png", "jpeg" };
        private int _MaxAllowedSize = 2097152;

        public SubscribersController(ApplicationDbContext context, IDataProtectionProvider dataProtector, IMapper mapper, IWebHostEnvironment webHostEnvironment, IEmailSender emailSender, IEmailBodyBuilder emailBodyBuilder)
        {
            _context = context;
            _dataProtector = dataProtector.CreateProtector("MySecureKey");
            _mapper = mapper;
            _webHostEnvironment = webHostEnvironment;
            _emailSender = emailSender;
            _emailBodyBuilder = emailBodyBuilder;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Search(SearchFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var subscriber = _context.Subscribers
                            .SingleOrDefault(s => s.Email == model.Value
                            || s.NationalId == model.Value
                            || s.MobileNumber == model.Value);

            var viewModel = _mapper.Map<SubscriberSearchResultViewModel>(subscriber);
            if (subscriber is not null)
                viewModel.Key = _dataProtector.Protect(subscriber.Id.ToString());

            return PartialView("_Result", viewModel);
        }

        public IActionResult Create()
        {
            var viewModel = PopulateViewModel();

            return View("Form", viewModel);
        }

        [AjaxOnly]
        public IActionResult GetAreas(int governorateId)
        {
            var areas = _context.Areas
                .Where(a => a.GovernorateId == governorateId && !a.IsDeleted)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name
                })
                .OrderBy(g => g.Text)
                .ToList();

            return Ok(_mapper.Map<IEnumerable<SelectListItem>>(areas));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubscriberFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", PopulateViewModel(model));

            var subscriber = _mapper.Map<Subscriber>(model);

            if (model.Image is not null)
            {
                var extension = Path.GetExtension(model.Image.FileName);
                if (!_allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(nameof(model.Image), Errors.NotAllowedExtension);
                    return View("Form", PopulateViewModel(model));
                }

                if (model.Image.Length > _MaxAllowedSize)
                {
                    ModelState.AddModelError(nameof(model.Image), Errors.MaxSize);
                    return View("Form", PopulateViewModel(model));
                }
                var imageName = $"{Guid.NewGuid()}{extension}";

                var path = Path.Combine($"{_webHostEnvironment.WebRootPath}/images/subscribers", imageName);
                var thumbPath = Path.Combine($"{_webHostEnvironment.WebRootPath}/images/subscribers/thumb", imageName);

                using var stream = System.IO.File.Create(path);
                await model.Image.CopyToAsync(stream);
                stream.Dispose();

                subscriber.ImageUrl = $"/images/subscribers/{imageName}";
                subscriber.ImageThumbnailUrl = $"/images/subscribers/thumb/{imageName}";

                using var image = Image.Load(model.Image.OpenReadStream());
                var ratio = (float)image.Width / 200;
                var height = image.Height / ratio;
                image.Mutate(i => i.Resize(width: 200, height: (int)height));
                image.Save(thumbPath);
            }
            subscriber.CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

            Subscription subscription = new()
            {
                CreatedById = subscriber.CreatedById,
                CreatedOn = subscriber.CreatedOn,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1)
            };

            subscriber.Subscriptions.Add(subscription);

            _context.Add(subscriber);
            _context.SaveChanges();

            //TODO Send welcome email
            var placeholders = new Dictionary<string, string>()
                {
                    { "ImageUrl","https://res.cloudinary.com/devcreed/image/upload/v1668739431/icon-positive-vote-2_jcxdww.svg" },
                    { "header",$"Hey {subscriber.FirstName} {subscriber.LastName}," },
                    { "body",$"welcome {subscriber.FirstName} Thanks for join us" },
                };

            var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Notification, placeholders);

            BackgroundJob.Enqueue(() => _emailSender.SendEmailAsync(model.Email, "Welcome to Gazify", body));

            var subscriberId = _dataProtector.Protect(subscriber.Id.ToString());

            return RedirectToAction(nameof(Details), new { id = subscriberId });
        }

        public IActionResult Details(string id)
        {
            var subscriberId = int.Parse(_dataProtector.Unprotect(id));
            var subscriber = _context.Subscribers
                .Include(s => s.Governorate)
                .Include(s => s.Area)
                .Include(s => s.Subscriptions)
                .SingleOrDefault(s => s.Id == subscriberId);

            if (subscriber == null)
                return NotFound();

            var viewModel = _mapper.Map<SubscriberViewModel>(subscriber);
            viewModel.Key = id;

            return View(viewModel);
        }


        [HttpGet]
        public IActionResult Edit(string id)
        {
            var subscriberId = int.Parse(_dataProtector.Unprotect(id));
            var subscriber = _context.Subscribers.Find(subscriberId);
            if (subscriber is null)
                return NotFound();

            var model = _mapper.Map<SubscriberFormViewModel>(subscriber);
            var viewModel = PopulateViewModel(model);
            viewModel.Key = id;

            return View("Form", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SubscriberFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", PopulateViewModel(model));

            var subscriberId = int.Parse(_dataProtector.Unprotect(model.Key!));

            var subscriber = _context.Subscribers.Find(subscriberId);

            if (subscriber is null)
                return NotFound();

            if (model.Image is not null)
            {
                if (!string.IsNullOrEmpty(model.ImageUrl))
                {
                    var oldImagePath = $"{_webHostEnvironment.WebRootPath}{subscriber.ImageUrl}";
                    var oldThumbPath = $"{_webHostEnvironment.WebRootPath}{subscriber.ImageThumbnailUrl}";

                    if (System.IO.File.Exists(oldImagePath))
                        System.IO.File.Delete(oldImagePath);

                    if (System.IO.File.Exists(oldThumbPath))
                        System.IO.File.Delete(oldThumbPath);
                }
                var extension = Path.GetExtension(model.Image.FileName);
                if (!_allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(nameof(model.Image), Errors.NotAllowedExtension);
                    return View("Form", PopulateViewModel(model));
                }

                if (model.Image.Length > _MaxAllowedSize)
                {
                    ModelState.AddModelError(nameof(model.Image), Errors.MaxSize);
                    return View("Form", PopulateViewModel(model));
                }
                var imageName = $"{Guid.NewGuid()}{extension}";

                var path = Path.Combine($"{_webHostEnvironment.WebRootPath}/images/subscribers", imageName);
                var thumbPath = Path.Combine($"{_webHostEnvironment.WebRootPath}/images/subscribers/thumb", imageName);

                using var stream = System.IO.File.Create(path);
                await model.Image.CopyToAsync(stream);
                stream.Dispose();

                model.ImageUrl = $"/images/subscribers/{imageName}";
                model.ImageThumbnailUrl = $"/images/subscribers/thumb/{imageName}";

                using var image = Image.Load(model.Image.OpenReadStream());
                var ratio = (float)image.Width / 200;
                var height = image.Height / ratio;
                image.Mutate(i => i.Resize(width: 200, height: (int)height));
                image.Save(thumbPath);
            }

            else if (!string.IsNullOrEmpty(subscriber.ImageUrl))
            {
                model.ImageUrl = subscriber.ImageUrl;
                model.ImageThumbnailUrl = subscriber.ImageThumbnailUrl;
            }
            //TODO Send welcome email
            var placeholders = new Dictionary<string, string>()
                {
                    { "ImageUrl","https://res.cloudinary.com/devcreed/image/upload/v1668739431/icon-positive-vote-2_jcxdww.svg" },
                    { "header",$"Hey {subscriber.FirstName} {subscriber.LastName}," },
                    { "body",$"welcome {subscriber.FirstName} Thanks for join us" },
                };

            var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Notification, placeholders);

            BackgroundJob.Enqueue(() => _emailSender.SendEmailAsync(model.Email, "Welcome to Gazify", body));

            subscriber = _mapper.Map(model, subscriber);
            subscriber.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            subscriber.LastUpdatedOn = DateTime.Now;

            _context.SaveChanges();

            return RedirectToAction(nameof(Details), new { id = model.Key });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RenewSubscription(string sKey)
        {
            var subscriberId = int.Parse(_dataProtector.Unprotect(sKey));

            var subscriber = _context.Subscribers
                                        .Include(s => s.Subscriptions)
                                        .SingleOrDefault(s => s.Id == subscriberId);

            if (subscriber is null)
                return NotFound();

            if (subscriber.IsBlackListed)
                return BadRequest();

            var lastSubscription = subscriber.Subscriptions.Last();

            var startDate = lastSubscription.EndDate < DateTime.Today
                            ? DateTime.Today
                            : lastSubscription.EndDate.AddDays(1);

            Subscription newSubscription = new()
            {
                CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value,
                CreatedOn = DateTime.Now,
                StartDate = startDate,
                EndDate = startDate.AddYears(1)
            };

            subscriber.Subscriptions.Add(newSubscription);

            _context.SaveChanges();

            //Send email and WhatsApp Message
            var placeholders = new Dictionary<string, string>()
            {
                { "imageUrl", "https://res.cloudinary.com/devcreed/image/upload/v1668739431/icon-positive-vote-2_jcxdww.svg" },
                { "header", $"Hello {subscriber.FirstName}," },
                { "body", $"your subscription has been renewed successfully through {newSubscription.EndDate.ToString("d MMM, yyyy")} 🎉🎉" }
            };

            var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Notification, placeholders);

            BackgroundJob.Enqueue(() => _emailSender.SendEmailAsync(
                subscriber.Email,
                "Gazify Subscription Renewal", body));

            //BackgroundJob.Schedule(() => _emailSender.SendEmailAsync(
            //    subscriber.Email,
            //    "Gazify Subscription Renewal", body),TimeSpan.FromMinutes(1));

            var viewModel = _mapper.Map<SubscriptionViewModel>(newSubscription);

            return PartialView("_SubscriptionRow", viewModel);
        }



        public IActionResult AllowNationalId(SubscriberFormViewModel model)
        {
            var subscriberId = 0;

            if (!string.IsNullOrEmpty(model.Key))
                subscriberId = int.Parse(_dataProtector.Unprotect(model.Key));

            var subscriber = _context.Subscribers.SingleOrDefault(b => b.NationalId == model.NationalId);
            var isAllowed = subscriber is null || subscriber.Id.Equals(subscriberId);

            return Json(isAllowed);
        }

        public IActionResult AllowMobileNumber(SubscriberFormViewModel model)
        {
            var subscriberId = 0;

            if (!string.IsNullOrEmpty(model.Key))
                subscriberId = int.Parse(_dataProtector.Unprotect(model.Key));

            var subscriber = _context.Subscribers.SingleOrDefault(b => b.MobileNumber == model.MobileNumber);
            var isAllowed = subscriber is null || subscriber.Id.Equals(subscriberId);

            return Json(isAllowed);
        }

        public IActionResult AllowEmail(SubscriberFormViewModel model)
        {
            var subscriberId = 0;

            if (!string.IsNullOrEmpty(model.Key))
                subscriberId = int.Parse(_dataProtector.Unprotect(model.Key));

            var subscriber = _context.Subscribers.SingleOrDefault(b => b.Email == model.Email);
            var isAllowed = subscriber is null || subscriber.Id.Equals(subscriberId);

            return Json(isAllowed);
        }

        private SubscriberFormViewModel PopulateViewModel(SubscriberFormViewModel? model = null)
        {
            SubscriberFormViewModel viewModel = model is null ? new SubscriberFormViewModel() : model;

            var governorates = _context.Governorates.Where(a => !a.IsDeleted).OrderBy(a => a.Name).ToList();

            viewModel.Governorates = _mapper.Map<IEnumerable<SelectListItem>>(governorates);

            if (model?.GovernorateId > 0)
            {
                var areas = _context.Areas
                    .Where(a => a.GovernorateId == model.GovernorateId && !a.IsDeleted)
                    .OrderBy(a => a.Name)
                    .ToList();

                viewModel.Areas = _mapper.Map<IEnumerable<SelectListItem>>(areas);
            }

            return viewModel;
        }
    }
}