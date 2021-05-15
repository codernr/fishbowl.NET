using Microsoft.AspNetCore.Components;

namespace Fishbowl.Net.Client.Components.States
{
    public partial class PlayerName
    {
        [Parameter]
        public EventCallback<string> OnPlayerNameSet { get; set; } = default!;

        public string Value { get; set; } = string.Empty;
    }
}