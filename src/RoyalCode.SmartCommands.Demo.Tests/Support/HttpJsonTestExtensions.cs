using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RoyalCode.SmartCommands.Demo.Tests.Support;

internal static class HttpJsonTestExtensions
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public static Task<T?> ReadApiJsonAsync<T>(this HttpContent content)
	{
		return content.ReadFromJsonAsync<T>(JsonOptions);
	}

	public static async Task<string> ReadApiTextAsync(this HttpContent content)
	{
		return await content.ReadAsStringAsync();
	}

	public static async Task AssertProblemAsync(
		this HttpResponseMessage response,
		HttpStatusCode expectedStatusCode,
		params string[] expectedFragments)
	{
		Assert.Equal(expectedStatusCode, response.StatusCode);

		var content = await response.Content.ReadApiTextAsync();
		Assert.False(string.IsNullOrWhiteSpace(content));

		foreach (var fragment in expectedFragments)
			Assert.Contains(fragment, content, StringComparison.OrdinalIgnoreCase);
	}
}
