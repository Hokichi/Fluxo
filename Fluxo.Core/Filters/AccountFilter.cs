using System;
using System.Collections.Generic;
using System.Text;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Filters
{
    public class AccountFilter
    {
        public string? Name { get; set; }
        public AccountType? Type { get; set; }
        public bool PinnedOnUIOnly { get; set; }
        public bool EnabledOnly { get; set; }
    }
}