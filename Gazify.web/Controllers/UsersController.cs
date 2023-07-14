using Gazify.Filters;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using System.Data;
using System.Text;
using System.Text.Encodings.Web;

namespace Gazify.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMapper _mapper;
        private readonly IEmailSender _emailSender;

        private readonly IEmailBodyBuilder _emailBodyBuilder;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IMapper mapper, IEmailSender emailSender, IEmailBodyBuilder emailBodyBuilder)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _mapper = mapper;
            _emailSender = emailSender;
            _emailBodyBuilder = emailBodyBuilder;
        }


        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var viewModel = _mapper.Map<IEnumerable<UserViewModel>>(users);
            return View(viewModel);
        }

        [HttpGet]
        [AjaxOnly]
        public async Task<IActionResult> Create()
        {
            var viewModel = new UserFormViewModel
            {
                Roles = await _roleManager.Roles
                                .Select(r => new SelectListItem
                                {
                                    Text = r.Name,
                                    Value = r.Name
                                })
                                .ToListAsync()
            };

            return PartialView("_Form", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            ApplicationUser user = new()
            {
                FullName = model.FullName,
                UserName = model.Username,
                Email = model.Email,
                CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value
            };

            var result = await _userManager.CreateAsync(user, model.Password!);

            if (result.Succeeded)
            {
                await _userManager.AddToRolesAsync(user, model.SelectedRoles);

                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code },
                    protocol: Request.Scheme);

                var placeholders = new Dictionary<string, string>()
                {
                    { "ImageUrl","https://res.cloudinary.com/devcreed/image/upload/v1668739431/icon-positive-vote-2_jcxdww.svg" },
                    { "header",$"Hey {user.FullName}," },
                    { "body","Please confirm your email" },
                    { "url",$"{HtmlEncoder.Default.Encode(callbackUrl!)}" },
                    { "linkTitle","Active Account!" }
                };

                var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Email, placeholders);

                await _emailSender.SendEmailAsync(user.Email, "Confirm your email", body);

                var viewModel = _mapper.Map<UserViewModel>(user);
                return PartialView("_UserRow", viewModel);
            }

            return BadRequest(string.Join(',', result.Errors.Select(e => e.Description)));
        }

        [HttpGet]
        [AjaxOnly]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user is null)
                return NotFound();

            var viewModel = _mapper.Map<UserFormViewModel>(user);

            viewModel.SelectedRoles = await _userManager.GetRolesAsync(user);
            viewModel.Roles = await _roleManager.Roles
                                .Select(r => new SelectListItem
                                {
                                    Text = r.Name,
                                    Value = r.Name
                                })
                                .ToListAsync();

            return PartialView("_Form", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var user = await _userManager.FindByIdAsync(model.Id!);

            if (user is null)
                return NotFound();

            user = _mapper.Map(model, user);
            user.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            user.LastUpdatedOn = DateTime.Now;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);

                var updatedRoles = !currentRoles.SequenceEqual(model.SelectedRoles);
                if (updatedRoles)
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRolesAsync(user, model.SelectedRoles);

                }

                var viewModel = _mapper.Map<UserViewModel>(user);
                return PartialView("_UserRow", viewModel);
            }

            return BadRequest(string.Join(',', result.Errors.Select(e => e.Description)));
        }

        [HttpGet]
        [AjaxOnly]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();

            var viewModel = new ResetPasswordFormViewModel { Id = user.Id };
            return PartialView("_ResetPasswordForm", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var user = await _userManager.FindByIdAsync(model.Id);

            if (user is null)
                return NotFound();

            var currentPasswordHash = user.PasswordHash;

            await _userManager.RemovePasswordAsync(user);

            var result = await _userManager.AddPasswordAsync(user, model.Password);

            if (result.Succeeded)
            {
                user.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                user.LastUpdatedOn = DateTime.Now;

                await _userManager.UpdateAsync(user);

                var viewModel = _mapper.Map<UserViewModel>(user);

                return PartialView("_UserRow", viewModel);
            }

            user.PasswordHash = currentPasswordHash;
            await _userManager.UpdateAsync(user);

            return BadRequest(string.Join(',', result.Errors.Select(e => e.Description)));
        }


        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();

            user.IsDeleted = !user.IsDeleted;
            user.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            user.LastUpdatedOn = DateTime.Now;

            await _userManager.UpdateAsync(user);

            return Ok(user.LastUpdatedOn.ToString());
        }

        public async Task<IActionResult> AllowUserName(UserFormViewModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            var isAllowed = user is null || user.Id.Equals(model.Id);

            return Json(isAllowed);
        }

        public async Task<IActionResult> AllowEmail(UserFormViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            var isAllowed = user is null || user.Id.Equals(model.Id);

            return Json(isAllowed);
        }
    }
}