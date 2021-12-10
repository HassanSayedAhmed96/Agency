using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Agency.ViewModel
{
    public class ChatViewModel
    {
        public int id { get; set; }
        public int? from_user { get; set; }
        public int? to_user { get; set; }
        public string strings_to_user { get; set; }
        public string strings_from_user { get; set; }
        public string image { get; set; }
        public string message { get; set; }
        public string message_class { get; set; }
        public DateTime? created_at { get; set; }
        public string string_created_at { get; set; }
    }
}