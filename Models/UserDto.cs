using System.Collections.Generic;
using System;

namespace server.Models
{
    // A Data Transfer Object for User information, ensuring sensitive data is not exposed.
    public class UserDto
    {
        public string Id { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public List<string> Roles { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
    }
} 