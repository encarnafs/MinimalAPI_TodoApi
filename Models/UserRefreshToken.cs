namespace TodoApi.Models;
public class UserRefreshToken
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string Token { get; set; } // Un GUID o string aleatorio
    public DateTime ExpiryDate { get; set; }
}