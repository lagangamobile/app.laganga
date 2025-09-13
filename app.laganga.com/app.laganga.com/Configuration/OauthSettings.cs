namespace app.laganga.com.Configuration
{
    public class OauthSettings
    {
        public string Authority { get; set; }
        public string[] Audience { get; set; }
        public string GrantType { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }
}
