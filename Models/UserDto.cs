using System.Collections.Generic;
using System;

namespace GurabaFiDunya.Models
{
    // A Data Transfer Object for User information, ensuring sensitive data is not exposed.
    public class UserDto
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public List<string> Roles { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> FavoriteReciters { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
} 