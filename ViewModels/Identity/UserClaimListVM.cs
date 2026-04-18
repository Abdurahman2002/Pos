namespace NewsApp2.ViewModels.Identity
{
    public class UserClaimListVM
    {
        public string UserId { get; set; } = string.Empty;
        public List<UserClaimVM> Claims { get; set; } = new();

    }
}
