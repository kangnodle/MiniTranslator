using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace MiniTranslator.Wpf.Controls;

/// <summary>Announces changing status text without moving keyboard focus.</summary>
public sealed class LiveTextBlock : TextBlock
{
    static LiveTextBlock()
    {
        TextProperty.OverrideMetadata(typeof(LiveTextBlock), new FrameworkPropertyMetadata(string.Empty, (d, _) =>
        {
            if (!string.IsNullOrEmpty(((LiveTextBlock)d).Text)
                && AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            {
                UIElementAutomationPeer.CreatePeerForElement((LiveTextBlock)d)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }));
    }
}
