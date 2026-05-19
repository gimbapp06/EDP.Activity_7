// =========================================
// Models/ApiResponse.cs — Shared Response
// =========================================
namespace ThbmsApi.Models
{
    /// <summary>
    /// Standard JSON response returned by all endpoints.
    /// Matches the PHP respond() helper: { success, message, ...data }
    /// </summary>
    public class ApiResponse
    {
        public bool   Success { get; set; }
        public string Message { get; set; } = "";
        public object? Data   { get; set; }

        public static ApiResponse Ok(string message, object? data = null)
            => new() { Success = true,  Message = message, Data = data };

        public static ApiResponse Fail(string message)
            => new() { Success = false, Message = message };
    }
}
