using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PSRes.Models
{
    public class EditUser
    {

        public Guid Id { get; set; }

        public string Name { get; set; }

       
        [EmailAddress(ErrorMessage = "Invalid Email")]
        public string Email { get; set; }

        
        public string Password { get; set; }

        public bool EmailConfirmed { get; set; }

        public List<string> RolesAssigned { get; set; }

        public List<string> RolesAvailable { get; set; }

        public string RolesAssignedSerial { get; set; } = null!;

        public string FirstName { get; set; }

        public string LastName { get; set; }

    }
}

