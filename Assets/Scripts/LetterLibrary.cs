public enum LetterSender { Neighbor, Customer, Aristocrat, Royal, Brigand, Landlord, Aunt, Rival }
public enum RepTier      { Low, Mid, High }

public struct LetterContent
{
    public LetterSender sender;
    public string       from;      // "Mrs. Hensley, next door"
    public string       subject;   // "Complaints from the alley"
    public string       body;      // main typewriter body

    public LetterContent(LetterSender sender, string from, string subject, string body)
    {
        this.sender  = sender;
        this.from    = from;
        this.subject = subject;
        this.body    = body;
    }
}

/// <summary>
/// Static content bank for all morning letters. Picked by day + reputation tier.
/// Day 1 is a shared intro letter; days 2–7 have Low/Mid/High variants.
/// Rent-reminder letters (days 3 and 6) are separate and appended after the
/// reputation-reactive letter on those mornings.
/// </summary>
public static class LetterLibrary
{
    public static RepTier GetRepTier(int reputation)
    {
        if (reputation >= GameManager.ROYAL_REP_MIN) return RepTier.High;
        if (reputation >= GameManager.RIVAL_REP_MIN) return RepTier.Mid;
        return RepTier.Low;
    }

    public static LetterContent GetMorningLetter(int day, RepTier tier)
    {
        if (day == 1) return IntroLetter;
        if (day >= 2 && day <= 7) return MorningLetters[day - 2][(int)tier];
        return IntroLetter;
    }

    public static LetterContent GetRentReminder(int day, int amount)
    {
        string body = day == 3
            ? $"Just a reminder, ducky. Rent of {amount}g is due at sundown. "
              + "Mr. Grimsby don't take excuses. He takes coin. Or fingers. "
              + "I've seen him take both from the cobbler on Thistle Lane.\n\n"
              + "Don't be the cobbler."
            : $"Last warning, pet. {amount}g by sundown. "
              + "Mr. Grimsby says if you miss this one, the gnomes come at dawn "
              + "with their little brass hammers and their little brass contracts. "
              + "You've come so far. Please don't let this be how it ends.";
        return new LetterContent(
            LetterSender.Landlord,
            "Mrs. Hensley (passing the landlord's note)",
            day == 3 ? "Rent due tonight" : "FINAL rent warning",
            body);
    }

    // ── Content ─────────────────────────────────────────────────

    private static readonly LetterContent IntroLetter = new LetterContent(
        LetterSender.Aunt,
        "Aunt Wrenna (posthumous)",
        "The shop is yours now",
        "My dearest,\n\n"
        + "If this owl reaches you, it means I've finally gone off to pester the stars. "
        + "The shop — and the rent that comes with it — is yours.\n\n"
        + "Some things you'll want to know:\n"
        + "• Mr. Grimsby collects rent every third night. 250g the first time, "
        + "400g the second. Miss either and the gnomes will come for the deeds.\n"
        + "• Ashenbury has a long memory and a louder mouth. Every wand you sell "
        + "writes a line in the town's gossip. Good work is noticed. Bad work <i>is also noticed.</i>\n"
        + "• You have seven days to make a name for yourself. What kind of name is up to you.\n\n"
        + "I left something for you under the third floorboard. Don't tell your mother.\n\n"
        + "All my love, and some of my trouble,\n"
        + "Wrenna");

    // [day - 2][rep tier] → letter
    private static readonly LetterContent[][] MorningLetters = new LetterContent[][]
    {
        // ── DAY 2 ─────────────────────────────────────────────────
        new LetterContent[]
        {
            // Low rep
            new LetterContent(
                LetterSender.Neighbor,
                "Mrs. Hensley, next door",
                "We need to talk about yesterday",
                "Pet,\n\n"
                + "Mrs. Abernathy's cat came home <b>blue</b>. She says it was your wand. "
                + "I'm not saying it was your wand. I'm just saying the cat is blue and "
                + "she's showing it to <i>everyone.</i>\n\n"
                + "You've got the whole week to sort yourself out. "
                + "Start sooner rather than later."),
            // Mid rep
            new LetterContent(
                LetterSender.Neighbor,
                "Mrs. Hensley, next door",
                "A curious start",
                "Pet,\n\n"
                + "I heard you sold a wand yesterday. To someone I've never seen before. "
                + "That's two things more interesting than this street has had in a year.\n\n"
                + "Keep it up. Just don't keep it <i>too</i> up — people get jealous."),
            // High rep
            new LetterContent(
                LetterSender.Neighbor,
                "Mrs. Hensley, next door",
                "Bragging on your behalf",
                "Pet,\n\n"
                + "I've told every woman at market about your shop. You owe me nothing. "
                + "It's what neighbours do.\n\n"
                + "(A man in velvet asked which door was yours. I pointed. "
                + "You're welcome.)")
        },

        // ── DAY 3 ─────────────────────────────────────────────────
        new LetterContent[]
        {
            // Low rep
            new LetterContent(
                LetterSender.Customer,
                "A disappointed customer",
                "This is not what I asked for",
                "Wandmaker,\n\n"
                + "The wand you sold me does not do what you said. It does something <i>else.</i> "
                + "I am writing this from a tree I did not intend to climb.\n\n"
                + "I want my coin back. I'll settle for a working wand. "
                + "I will <b>not</b> settle for another day in this tree."),
            // Mid rep
            new LetterContent(
                LetterSender.Customer,
                "A thoughtful customer",
                "It almost fit",
                "Wandmaker,\n\n"
                + "The wand works. It doesn't <i>sing,</i> but it works. "
                + "I'd have paid twice as much for something that sang.\n\n"
                + "Next time, perhaps, ask one more question before you pick the wood."),
            // High rep
            new LetterContent(
                LetterSender.Aristocrat,
                "Lord Verrick of Thornhedge",
                "An inquiry, in confidence",
                "Wandmaker,\n\n"
                + "A friend of a friend showed me the wand you crafted for him. "
                + "It sang, I am told, in <i>three</i> registers. I should very much like to meet you.\n\n"
                + "I will send a proper invitation in due course. Be ready.")
        },

        // ── DAY 4 ─────────────────────────────────────────────────
        new LetterContent[]
        {
            // Low rep
            new LetterContent(
                LetterSender.Brigand,
                "Grey Finch (nom de plume)",
                "Dark work wants a dark hand",
                "Friend,\n\n"
                + "Word travels. The cat. The tree. The general <i>chaos</i> of your bench.\n\n"
                + "We have work for a hand like yours. Wands for jobs the proper shops "
                + "won't touch. Discreet. Well paid.\n\n"
                + "If you're curious, leave your window open tonight. "
                + "We'll know what that means."),
            // Mid rep
            new LetterContent(
                LetterSender.Customer,
                "A cautious customer",
                "A small refund, if you would",
                "Wandmaker,\n\n"
                + "The wand is <i>fine.</i> Fine is not what I paid for, but fine is what I have. "
                + "A partial refund would put the matter to rest, and your name would rest with it.\n\n"
                + "Think on it. I'm patient, but not infinitely so."),
            // High rep
            new LetterContent(
                LetterSender.Aristocrat,
                "Baronet Dellamare",
                "A proposal",
                "Wandmaker,\n\n"
                + "I won't waste your time. I need a wand suited to a temperament nobody understands "
                + "— my daughter's. If you can do for her what you did for Lord Verrick's hunting friend, "
                + "I will pay whatever you like.\n\n"
                + "The week is young. So is she.")
        },

        // ── DAY 5 ─────────────────────────────────────────────────
        new LetterContent[]
        {
            // Low rep
            new LetterContent(
                LetterSender.Brigand,
                "Grey Finch",
                "Second knock",
                "Friend,\n\n"
                + "Window stayed shut. We don't hold it against you.\n\n"
                + "But the rent's coming. And our offer still stands. "
                + "Two silver each for a <i>special</i> wand, no questions, no paper trail.\n\n"
                + "Sleep on it. We'll ask again."),
            // Mid rep
            new LetterContent(
                LetterSender.Rival,
                "Marwyn of Oakscroft Wands",
                "Neighbourly advice",
                "Colleague,\n\n"
                + "You've done well for a first week. I'll be honest — better than I expected.\n\n"
                + "I mention only because I know of two shops eyeing the lot across from yours. "
                + "If you keep rising, you'll have company. Neighbourly company. "
                + "The kind that watches your hours."),
            // High rep
            new LetterContent(
                LetterSender.Royal,
                "Archmage Sellon, Court Wand-Tuner",
                "The Crown has noticed",
                "Master Wandmaker,\n\n"
                + "Your name has reached chambers that do not ordinarily learn the names of "
                + "Ashenbury shopkeeps. This is not a summons. It is barely a letter.\n\n"
                + "It is, however, a <i>record.</i> Keep doing what you are doing. "
                + "Records have their own momentum.")
        },

        // ── DAY 6 ─────────────────────────────────────────────────
        new LetterContent[]
        {
            // Low rep
            new LetterContent(
                LetterSender.Brigand,
                "Grey Finch",
                "Final offer",
                "Friend,\n\n"
                + "Rent tonight. We both know the math.\n\n"
                + "Come to the Drowned Moth after sundown. Bring a wand — any wand. "
                + "We'll take care of the rest. You'll sleep tomorrow. "
                + "Which is more than the honest kind can say."),
            // Mid rep
            new LetterContent(
                LetterSender.Neighbor,
                "Mrs. Hensley, next door",
                "Just so you hear it from me first",
                "Pet,\n\n"
                + "A second shop is definitely coming. Marwyn's cousin, I think. "
                + "Nothing's built yet. But the <i>sign's</i> been commissioned, and signs are serious.\n\n"
                + "Don't panic. Just be excellent. You've been doing fine at that."),
            // High rep
            new LetterContent(
                LetterSender.Royal,
                "The Chamberlain of the Royal Atelier",
                "An invitation is forthcoming",
                "Master Wandmaker,\n\n"
                + "A proper messenger will arrive tomorrow. Velvet, horses, an unsubtle amount of ceremony.\n\n"
                + "I write ahead so you will have your best apron clean. "
                + "Do not pretend to be surprised. Do be gracious.")
        },

        // ── DAY 7 ─────────────────────────────────────────────────
        new LetterContent[]
        {
            // Low rep
            new LetterContent(
                LetterSender.Brigand,
                "Grey Finch",
                "Come with us",
                "Friend,\n\n"
                + "Whatever happens today, the shop is finished. The town's made up its mind.\n\n"
                + "Ours hasn't. Come to the alley behind the Drowned Moth at dusk. "
                + "Bring the wand-knife. Leave everything else."),
            // Mid rep
            new LetterContent(
                LetterSender.Rival,
                "Marwyn of Oakscroft Wands",
                "About that new shop",
                "Colleague,\n\n"
                + "The sign goes up tomorrow. <i>Oakscroft & Son, est. today.</i>\n\n"
                + "Welcome to the business. It is not as friendly as I made it sound on day five."),
            // High rep
            new LetterContent(
                LetterSender.Royal,
                "Herald of the Kingdom",
                "By Royal Decree",
                "Master Wandmaker,\n\n"
                + "At the close of this day, an escort will arrive at your door. "
                + "You will not need to pack heavily. The palace has everything a wandmaker requires. "
                + "Everything, that is, except the wandmaker.\n\n"
                + "<i>Long may the Crown remember your name.</i>")
        }
    };
}
