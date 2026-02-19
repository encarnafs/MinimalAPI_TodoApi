
namespace TodoApi;
public class UserRefreshToken
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string Token { get; set; } // Un GUID o string aleatorio
    public DateTime ExpiryDate { get; set; }
}