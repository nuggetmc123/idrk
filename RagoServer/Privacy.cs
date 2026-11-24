using System;

namespace RagoServer
{
    /// <summary>
    /// The public privacy policy, served by the game server at /privacy (no login). Who runs the game and how to reach
    /// them come from the server's environment, so nothing personal is in the code:
    ///   RAGO_OWNER   - the person or company responsible (the "data controller"), e.g. their full legal name
    ///   RAGO_CONTACT - an email address for privacy questions and deletion requests
    /// </summary>
    static partial class Program
    {
        const string PrivacyUpdated = "November 24, 2026";

        static string PrivacyPage()
        {
            string owner = Environment.GetEnvironmentVariable("RAGO_OWNER");
            string contact = Environment.GetEnvironmentVariable("RAGO_CONTACT");
            string who = string.IsNullOrWhiteSpace(owner) ? "the developer of Rago Extract" : H(owner.Trim());
            string reach = string.IsNullOrWhiteSpace(contact)
                ? "through the contact details on Rago Extract's Meta Horizon Store page"
                : "by email at <a href='mailto:" + H(contact.Trim()) + "'>" + H(contact.Trim()) + "</a>";
            string body = @"
<h1>Rago Extract - Privacy Policy</h1>
<p class=dim>Last updated: " + PrivacyUpdated + @"</p>
<p>Rago Extract is a VR extraction shooter. This page explains what information the game uses, why, who it's shared with, and the choices you have. Rago Extract is run by " + who + @" (""we""). You can reach us " + reach + @".</p>

<h2>The short version</h2>
<ul>
<li>We only use what the game needs to work: your in-game callsign, a random player ID, a device ID, and what you do in multiplayer (voice chat, movement, trades).</li>
<li>Voice chat and your movements are passed live to the other players in your game. <b>We don't record or store voice chat.</b></li>
<li>We don't show ads, don't sell your data, and don't use analytics or tracking tools.</li>
<li>Purchases go through Meta (or Steam on PC). We never see your payment details.</li>
</ul>

<h2>What we collect and why</h2>
<table>
<tr><th>Information</th><th>Why</th><th>Kept</th></tr>
<tr><td><b>Callsign</b> (the name you pick in the game)</td><td>So other players can see, find and invite you, and for the player market.</td><td>On our game server while the game is running, until you ask us to delete it.</td></tr>
<tr><td><b>Player ID</b> (a random code the game makes on your headset) and a <b>device ID</b></td><td>To recognise your account between sessions, keep your black-market shop and sales, and enforce bans on cheaters.</td><td>On our game server, same as above. Ban records are kept while the ban lasts.</td></tr>
<tr><td><b>IP address</b> and game version</td><td>Needed to connect you to other players. Short-term server logs help us fix problems and stop abuse.</td><td>Logs are kept for 30 days at most, then deleted.</td></tr>
<tr><td><b>Voice chat</b> (microphone)</td><td>Only when you use voice chat: your voice is sent live to players near you in the same game.</td><td><b>Not stored.</b> It's relayed and then gone.</td></tr>
<tr><td><b>Headset and hand positions</b></td><td>Sent live to other players in your game so they can see your body and what you're doing.</td><td>Not stored.</td></tr>
<tr><td><b>Black market listings, trades and sales</b></td><td>To keep your shop open while you're offline and give you money or items from sales.</td><td>On our game server until the listing is sold or removed. A record of sold items is kept so nothing can be bought twice.</td></tr>
<tr><td><b>Your view, while being watched</b></td><td>The owner can watch a player's first-person view live (a small picture about twice a second) to check for cheating and keep the game safe. Your watch shows SPECTATED while this is happening.</td><td>Not stored. Only the latest picture is kept in memory while someone is watching.</td></tr>
<tr><td><b>Moderation records</b> (bans, wipes, Market kicks and the reason; moderator requests)</td><td>To keep the game fair and safe.</td><td>On our game server while they're relevant.</td></tr>
<tr><td><b>Meta account data</b>: your Meta User ID, your add-on purchases, and your age group</td><td>To unlock DLC packs you bought (and restore them after a reinstall), and to adjust features such as voice chat for younger players.</td><td>Not stored on our server. Checked with Meta when needed.</td></tr>
</table>

<h2>Stored only on your headset or PC</h2>
<p>Your progress (stash, money, level, gear, bunker), your settings, and any videos you record with the in-game Camera are saved on your own device. Videos go to your device's Movies or Videos folder and are never uploaded by us.</p>

<h2>Who we share it with</h2>
<ul>
<li><b>Other players</b>: your callsign, voice (when you use voice chat), your movements, and your shop listings are visible to the players you play or trade with.</li>
<li><b>Hetzner Online GmbH</b> (in the EU) hosts our game server. It runs the server for us and doesn't use your data for anything else.</li>
<li><b>Meta</b> (and <b>Valve / Steam</b> on PC) handle purchases and your platform account under their own privacy policies.</li>
<li><b>The law</b>: we'd only share information if we're legally required to, and only the minimum needed.</li>
</ul>
<p>We don't sell or rent your information, and we don't share it with advertisers.</p>

<h2>Children</h2>
<p>Rago Extract is a shooter with realistic weapons and is meant for teens and adults. It isn't made for children under 13, and we don't knowingly collect personal information from children under 13. Meta tells us a player's age group, and younger players get a restricted experience (for example, no voice chat with strangers). If you think a child under 13 has used the game, contact us and we'll delete their information.</p>

<h2>Your choices and rights</h2>
<ul>
<li><b>Voice chat</b> is optional. You can turn your microphone off in the game or in your headset's settings.</li>
<li><b>Access or delete your data</b>: contact us " + reach + @" with your callsign, and we'll send you what we hold or delete it (ban records may be kept to stop the same account being used again).</li>
<li>Depending on where you live (for example the EU, the UK or California), you may have extra rights, like correcting your data or objecting to its use. Contact us and we'll help.</li>
</ul>

<h2>Security</h2>
<p>Our server keeps as little information as it can. The owner panel is protected by a password, and only the owner and approved moderators can take action on accounts.</p>

<h2>Changes</h2>
<p>If we change this policy, we'll update this page and the date at the top. Big changes will also be mentioned in the game's patch notes.</p>

<h2>Contact</h2>
<p>Questions or requests: " + reach + @".</p>";
            return Page("Rago Extract - Privacy Policy", body);
        }
    }
}
