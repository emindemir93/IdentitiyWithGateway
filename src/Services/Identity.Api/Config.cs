using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Identity.Api;

public static class Config
{
    // public static IEnumerable<IdentityResource> IdentityResources =>
    //     new IdentityResource[]
    //     {
    //         new IdentityResources.OpenId(),
    //         new IdentityResources.Profile(),
    //     };

    // public static IEnumerable<ApiScope> ApiScopes =>
    //     new ApiScope[]
    //     {
    //         new ApiScope("bitedavi.api", "bitedavi api"),
    //     };

    // public static IEnumerable<Client> Clients =>
    //     new Client[]
    //     {
    //         // machine to machine client
    //         new Client
    //         {
    //             ClientId = "client",
    //             ClientSecrets = { new Secret("secret".Sha256()) },

    //             AllowedGrantTypes = GrantTypes.ClientCredentials,
    //             // scopes that client has access to
    //             AllowedScopes = { "bitedavi.api" }
    //         },

    //         // interactive ASP.NET Core Web App
    //         new Client
    //         {
    //             ClientId = "web",
    //             ClientSecrets = { new Secret("secret".Sha256()) },

    //             AllowedGrantTypes = GrantTypes.Code,
                    
    //             // where to redirect to after login
    //             RedirectUris = { "https://localhost:5002/signin-oidc" },

    //             // where to redirect to after logout
    //             PostLogoutRedirectUris = { "https://localhost:5002/signout-callback-oidc" },
                
    //             AllowOfflineAccess = true,
                
    //             AllowedScopes = new List<string>
    //             {
    //                 IdentityServerConstants.StandardScopes.OpenId,
    //                 IdentityServerConstants.StandardScopes.Profile,
    //                 "bitedavi.api",
    //             }
    //         }
    //     };
   
    public static IEnumerable<IdentityResource> IdentityResources =>
        new IdentityResource[]
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new ApiScope[]
        {
            new ApiScope("scope1"),
            new ApiScope("scope2"),
            new ApiScope("Identity"),
        };

    public static IEnumerable<Client> Clients =>
        new Client[]
        {
            // m2m client credentials flow client
            new Client
            {
                ClientId = "spa",
                ClientName = "SPA Client",
                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                ClientSecrets = { new Secret("511536EF-F270-4058-80CA-1C89C192F69A".Sha256()) },
                AllowedScopes = { "scope1", "Identity" },
                RequireClientSecret = false,

            }
        };
    public static IEnumerable<ApiResource> GetApis()
        {
            return new List<ApiResource>
            {
                new ApiResource("Identity.Api", "Identity"),
            };
        }
}
