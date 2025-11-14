using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace EseeBridge.Auth;

// Custom authentication handler that processes Basic Authentication.
// Inherits from AuthenticationHandler<TOptions> where TOptions is AuthenticationSchemeOptions.
public class BasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
        ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    // Core method that performs authentication logic on each request.
    // Called automatically when ASP.NET Core needs to authenticate a request.
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        try
        {
            // Step 1: Check if the Authorization header exists in the request.
            if (!Request.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues value))
            {
                // No credentials provided, authentication fails.
                return AuthenticateResult.Fail("Missing Authorization Header");
            }

            // Step 2: Retrieve the Authorization header value.
            var authorizationHeader = value.ToString();

            // Step 3: Parse the header to separate scheme and parameter.
            // The expected format is: "Basic base64encodedCredentials"
            if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var headerValue))
            {
                // If parsing fails, the header is considered invalid and authentication fails.
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }

            // Step 4: Verify that the scheme is "Basic" (case-insensitive).
            if (!"Basic".Equals(headerValue.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                // If not Basic scheme, authentication is not applicable.
                return AuthenticateResult.Fail("Invalid Authorization Scheme");
            }

            // Step 5: Decode the Base64-encoded credentials ("username:password").
            var credentialBytes = Convert.FromBase64String(headerValue.Parameter!);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);

            // Step 6: Validate that the decoded string contains exactly username and password.
            if (credentials.Length != 2)
            {
                // If not, the credentials are invalid and authentication fails.
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }

            // Step 7: Extract username (here Email) and password from credentials.
            var username = credentials[0];
            var password = credentials[1];

            // Step 8: Query the database for the user by email.
            if (username != "esee-bridge" || password != "B!t9aziS")
            {
                // User not found or password incorrect.
                return AuthenticateResult.Fail("Invalid Username or Password");
            }

            // Step 9: Create claims that represent the user's identity and roles.
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, username), // Unique user ID
                new(ClaimTypes.Name, username) // Username or email
            };

            // Step 10: Create a ClaimsIdentity with the authentication scheme name.
            var claimsIdentity = new ClaimsIdentity(claims, Scheme.Name);

            // Step 11: Create ClaimsPrincipal that holds the ClaimsIdentity (and potentially multiple identities).
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            // Step 12: Create an AuthenticationTicket which encapsulates the user's identity (ClaimsPrincipal) and scheme info.
            // AuthenticationTicket is the object used by ASP.NET Core to store and
            // track the authenticated user’s ClaimsPrincipal during an authentication session.
            var authenticationTicket = new AuthenticationTicket(claimsPrincipal, Scheme.Name);

            // Step 14: Return success result with the AuthenticationTicket indicating successful authentication.
            return AuthenticateResult.Success(authenticationTicket);
        }
        catch
        {
            // If any unexpected error occurs during authentication, fail with a generic error.
            return AuthenticateResult.Fail("Error occurred during authentication");
        }
    }
}