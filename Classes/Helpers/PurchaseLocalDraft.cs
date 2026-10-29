namespace NewsApp2.Classes.Helpers
{
    /// <summary>
    /// Builds the browser localStorage key prefix used by the purchase invoice
    /// screen to keep an unsaved draft when the network drops or the session
    /// expires. The JavaScript layer appends a per-tab id to this prefix so two
    /// open tabs never overwrite each other's draft.
    /// </summary>
    public static class PurchaseLocalDraft
    {
        private const string KeyPrefix = "purchase.create.";

        public static string BuildBaseKey(string? userName)
        {
            var normalized = string.IsNullOrWhiteSpace(userName)
                ? "anonymous"
                : userName.Trim().ToLowerInvariant();

            return KeyPrefix + normalized;
        }
    }
}
