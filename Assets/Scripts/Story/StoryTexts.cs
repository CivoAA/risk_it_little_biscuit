using System.Collections.Generic;

/// <summary>
/// Alle Texte der Story: Gespraeche (Seite fuer Seite mit Sprecher und
/// Stimmung), Zwischenrufe im Lauf und die Bildunterschriften der Cutscenes.
///
/// Der deutsche Text hier ist nur der Rueckfall - gezeigt wird, was unter dem
/// Key in de.json / en.json steht. Beide Sprachdateien und diese Datei sind
/// aus einer gemeinsamen Vorlage entstanden; wer einen Text aendert, aendert
/// ihn in beiden JSONs (und hier, damit der Rueckfall stimmt).
///
/// Markup in den Seiten (siehe <see cref="DialogueText"/>): &lt;wave&gt;,
/// &lt;shake&gt;, &lt;hot&gt;, &lt;ice&gt;, &lt;jam&gt;, &lt;gold&gt;.
///
/// Gespraeche:
///   intro            nach der Intro-Cutscene im Hub
///   brief.[Plan]     vor dem ersten Story-Lauf einer Welt (am SPIELEN-Knopf)
///   defeat.[Plan]    im Lauf, sobald der Boss gefallen ist (neuer Splitter)
///   count.[1-4]      Pomo danach, je nach Zahl der Splitter
///   debrief.[Plan]   zurueck im Hub nach dem Sieg
///   allshards        im Hub, wenn alle vier Splitter da sind (Vulkan geht auf)
///   finale.a/b       in der Abschluss-Cutscene, vor und nach dem Klingeln
///   epilogue         im Hub nach dem Abspann
///   talk.*           Pomo im Hub ansprechen
/// </summary>
public static class StoryTexts
{
    public enum Who { Pomo, Player, KeksKoenig, Glutwurz, Eiskaiser, Gespenst, Squiddy, Verkohlter }

    public enum Mood { Normal, Sad, Happy, Shock, Ring }

    public struct Line
    {
        public Who Who;
        public Mood Mood;
        public string Key;
        public string Fallback;

        public string Text => Loc.Get(Key, Fallback);
    }

    private static Line L(Who who, Mood mood, string key, string fallback) =>
        new Line { Who = who, Mood = mood, Key = key, Fallback = fallback };

    private static readonly Dictionary<string, Line[]> Convos = new Dictionary<string, Line[]>
    {
        ["intro"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.intro.1", "Tick... tack... Na endlich. Du bist wach!"),
            L(Who.Pomo, Mood.Happy, "story.intro.2", "Ich bin Pomo. Küchenuhr. Tomate. Beides, ja – frag nicht."),
            L(Who.Pomo, Mood.Normal, "story.intro.3", "Spürst du, wie warm der Boden ist? Das ist der <hot>Große Ofen</hot>. Der <hot>Verkohlte</hot> heizt ihn an."),
            L(Who.Pomo, Mood.Sad, "story.intro.4", "Wenn er heiß genug ist, backt er alles schwarz. Die Küche. Den Wald. Dich. Mich."),
            L(Who.Pomo, Mood.Normal, "story.intro.5", "Sein Herz hat er in vier <hot>Glutsplitter</hot> zerbrochen. Vier Herrscher tragen sie jetzt – und drehen durch."),
            L(Who.Pomo, Mood.Normal, "story.intro.6", "Hol mir die Splitter. Mit allen vier geht die Ofentür auf. Die erkennt nämlich nur sein Herz."),
            L(Who.Pomo, Mood.Happy, "story.intro.7", "Und die Uhr, die du da draußen oben im Bild siehst? Das bin ich. Ich zähl mit. Nach zwanzig Minuten kommt der Boss."),
            L(Who.Player, Mood.Normal, "story.intro.8", "Bin dabei."),
            L(Who.Pomo, Mood.Happy, "story.intro.9", "So gefällt mir das. Die Reisekarte steht gleich da drüben. <wave>Risk it, little biscuit.</wave>"),
        },
        ["brief.World1"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.brief.World1.1", "Die Küche. Unsere gute alte Arbeitsplatte. Da oben regiert der <gold>Keks-König</gold>."),
            L(Who.Pomo, Mood.Happy, "story.brief.World1.2", "Früher war er eitel, aber harmlos. Hat den ganzen Tag seine Krone poliert."),
            L(Who.Pomo, Mood.Sad, "story.brief.World1.3", "Seit er den Splitter trägt, hat er nur noch ein Auge. Und das sieht überall Feinde."),
            L(Who.Pomo, Mood.Normal, "story.brief.World1.4", "Pass auf den <ice>Milchpanzer</ice> auf. Und lass dich nicht von Pfannkuchen einwickeln."),
        },
        ["defeat.World1"] = new[]
        {
            L(Who.KeksKoenig, Mood.Normal, "story.defeat.World1.1", "Mein Kopf... so leicht. So klar."),
            L(Who.KeksKoenig, Mood.Normal, "story.defeat.World1.2", "War ich... gemein? Sag nichts. Ich weiß es."),
            L(Who.KeksKoenig, Mood.Normal, "story.defeat.World1.3", "Nimm das heiße Ding. Ich will es nie wieder sehen."),
        },
        ["debrief.World1"] = new[]
        {
            L(Who.Pomo, Mood.Shock, "story.debrief.World1.1", "Der König hat sich entschuldigt? Bei DIR? Der hat sich noch nie bei irgendwem entschuldigt!"),
            L(Who.Pomo, Mood.Normal, "story.debrief.World1.2", "Gib her – uff, ist der heiß! Spürst du das? Er pocht. Wie ein Herz."),
        },
        ["brief.World2"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.brief.World2.1", "Der Kräuterwald hinterm Brotkasten. Da wohnt die <hot>Glutwurz</hot>, die älteste Wurzel der Küche."),
            L(Who.Pomo, Mood.Sad, "story.brief.World2.2", "Der Splitter steckt tief in ihren Wurzeln. Jetzt spuckt sie Feuer, und der ganze Wald riecht nach Toast."),
            L(Who.Pomo, Mood.Normal, "story.brief.World2.3", "Wenn sie tief Luft holt: raus aus der Linie. Glaub mir einfach."),
        },
        ["defeat.World2"] = new[]
        {
            L(Who.Glutwurz, Mood.Normal, "story.defeat.World2.1", "Kühl... endlich wieder kühl. Danke, kleines Ding."),
            L(Who.Glutwurz, Mood.Normal, "story.defeat.World2.2", "Hör zu. Der Splitter hat geflüstert. Immer dasselbe."),
            L(Who.Glutwurz, Mood.Normal, "story.defeat.World2.3", "<wave>»Hüte dich vor der Uhr, die nicht klingelt.«</wave>"),
            L(Who.Pomo, Mood.Shock, "story.defeat.World2.4", "...Tick. Äh. Gut gemacht."),
        },
        ["debrief.World2"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.debrief.World2.1", "Eine Uhr, die nicht klingelt? Alte Wurzeln reden viel, wenn der Tag lang ist."),
            L(Who.Pomo, Mood.Sad, "story.debrief.World2.2", "<shake>Tick-tick-tick.</shake> Das ist... nur meine Feder. Die ist alt. Egal."),
        },
        ["brief.World5"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.brief.World5.1", "Der Eisgletscher. Das ist das <ice>Gefrierfach</ice>, Krümel. Da war seit Jahren keiner mehr drin."),
            L(Who.Pomo, Mood.Happy, "story.brief.World5.2", "Dort regiert der <ice>Eiskaiser</ice>. Ein Pinguin aus Sesameis, stolz wie ein Sahnehäubchen."),
            L(Who.Pomo, Mood.Sad, "story.brief.World5.3", "Der Splitter schmilzt ihn von innen. Darum friert er alles ein – damit er selbst nicht zerläuft."),
            L(Who.Pomo, Mood.Happy, "story.brief.World5.4", "Nimm Schwung aufs Glatteis mit. Bremsen ist da drüben nur ein Gerücht."),
        },
        ["defeat.World5"] = new[]
        {
            L(Who.Eiskaiser, Mood.Normal, "story.defeat.World5.1", "Ich... schmelze. Es ist gar nicht so schlimm."),
            L(Who.Eiskaiser, Mood.Normal, "story.defeat.World5.2", "Sag meinen Pinguinen... Sorte Sesam. Die beste Sorte."),
            L(Who.Pomo, Mood.Sad, "story.defeat.World5.3", "Ruhe in Sahne, Majestät."),
        },
        ["debrief.World5"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.debrief.World5.1", "Merkst du's? Der Boden wird wärmer. Er weiß, dass wir kommen."),
            L(Who.Pomo, Mood.Sad, "story.debrief.World5.2", "Und er weiß, wer dich schickt."),
        },
        ["brief.World6"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.brief.World6.1", "Der Geisterwald. Ganz hinten in der <jam>Speisekammer</jam>. Da landet alles, was vergessen wurde."),
            L(Who.Pomo, Mood.Normal, "story.brief.World6.2", "Lebkuchen von vorletzter Weihnacht. Kürbisse von Halloween. Abgelaufen – aber nicht weg."),
            L(Who.Pomo, Mood.Normal, "story.brief.World6.3", "Etwas unter einem Laken trägt dort einen Splitter. Bleib im Licht."),
            L(Who.Pomo, Mood.Sad, "story.brief.World6.4", "Und Krümel... wenn da drin jemand über mich redet: Hör nicht hin."),
        },
        ["defeat.World6"] = new[]
        {
            L(Who.Squiddy, Mood.Happy, "story.defeat.World6.1", "Oh! Wo ist mein Laken hin? Ich bin gar nicht gruselig. Ich bin Traubengelee!"),
            L(Who.Squiddy, Mood.Normal, "story.defeat.World6.2", "Der Verkohlte war hier. Bei uns Vergessenen. Er hat immer dasselbe erzählt."),
            L(Who.Squiddy, Mood.Sad, "story.defeat.World6.3", "<wave>»Die Uhr hätte klingeln sollen. Zwölf Minuten. Sie hätte klingeln sollen.«</wave>"),
        },
        ["debrief.World6"] = new[]
        {
            L(Who.Pomo, Mood.Sad, "story.debrief.World6.1", "..."),
            L(Who.Pomo, Mood.Sad, "story.debrief.World6.2", "Tick."),
            L(Who.Pomo, Mood.Sad, "story.debrief.World6.3", "Er hat recht. Ich hätte klingeln sollen."),
            L(Who.Pomo, Mood.Sad, "story.debrief.World6.4", "Vor hundert Jahren war ich seine Uhr. Ein Blech Kekse, zwölf Minuten. Und ich bin... eingeschlafen. Meine Feder war müde."),
            L(Who.Pomo, Mood.Sad, "story.debrief.World6.5", "Die anderen hat jemand trotzdem rausgeholt. Ihn nicht. Er war hinters Blech gerutscht."),
            L(Who.Pomo, Mood.Sad, "story.debrief.World6.6", "Er hat da drin geglüht, Krümel. Hundert Jahre. Und auf mein Klingeln gewartet."),
            L(Who.Player, Mood.Normal, "story.debrief.World6.7", "..."),
            L(Who.Pomo, Mood.Normal, "story.debrief.World6.8", "Ich weiß. Ich hätt's dir früher sagen müssen. Aber jetzt weißt du, warum ich am Ende mitkomme."),
        },
        ["allshards"] = new[]
        {
            L(Who.Pomo, Mood.Shock, "story.allshards.1", "Vier. Alle vier! Hörst du das? Sie schlagen im Takt."),
            L(Who.Pomo, Mood.Normal, "story.allshards.2", "Die Ofentür erkennt sein Herz. Jetzt geht sie auf."),
            L(Who.Pomo, Mood.Normal, "story.allshards.3", "Der <hot>Vulkan</hot> ist offen, Krümel. Und diesmal komm ich mit."),
        },
        ["brief.World4"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.brief.World4.1", "Der Große Ofen. Hier hat alles angefangen."),
            L(Who.Pomo, Mood.Happy, "story.brief.World4.2", "Halt mich gut fest. Wenn ich runterfalle, zähl ich falsch."),
            L(Who.Pomo, Mood.Normal, "story.brief.World4.3", "Sein Herz schlägt noch, auch ohne die Splitter. Er wird versuchen, dich hineinzuziehen."),
            L(Who.Pomo, Mood.Sad, "story.brief.World4.4", "Wenn es zerbricht... dann klingle ich. Diesmal klingle ich."),
        },
        ["finale.a"] = new[]
        {
            L(Who.Verkohlter, Mood.Normal, "story.finale.a.1", "Es ist... kühl."),
            L(Who.Verkohlter, Mood.Normal, "story.finale.a.2", "Hundert Jahre hab ich gezählt. Jede Sekunde. Auf ein Klingeln gewartet."),
            L(Who.Pomo, Mood.Sad, "story.finale.a.3", "Ich weiß. Es tut mir so leid. Ich hätte klingeln müssen."),
            L(Who.Pomo, Mood.Normal, "story.finale.a.4", "Aber jetzt... jetzt bist du fertig."),
        },
        ["finale.b"] = new[]
        {
            L(Who.Verkohlter, Mood.Normal, "story.finale.b.1", "...Fertig gebacken."),
            L(Who.Verkohlter, Mood.Normal, "story.finale.b.2", "Danke, Uhr."),
        },
        ["epilogue"] = new[]
        {
            L(Who.Pomo, Mood.Happy, "story.epilogue.1", "Hörst du das? Tick, tack. Ich tick wieder ganz richtig."),
            L(Who.Pomo, Mood.Normal, "story.epilogue.2", "Der Ofen ist aus. Die Küche bleibt, wie sie ist. Krümelig."),
            L(Who.Pomo, Mood.Happy, "story.epilogue.3", "Aber Ärger gibt's hier immer. Wenn du Lust hast: Endlos-Modus. Ich zieh dich auf."),
            L(Who.Pomo, Mood.Happy, "story.epilogue.4", "Und Krümel? <wave>Danke, dass du's riskiert hast.</wave>"),
        },
        ["count.1"] = new[]
        {
            L(Who.Pomo, Mood.Happy, "story.count.1.1", "Das ist der erste Splitter! Komm heim, Krümel."),
        },
        ["count.2"] = new[]
        {
            L(Who.Pomo, Mood.Happy, "story.count.2.1", "Zwei von vier! Komm heim, ich halt dir den Platz warm."),
        },
        ["count.3"] = new[]
        {
            L(Who.Pomo, Mood.Happy, "story.count.3.1", "Drei! Nur noch einer, Krümel."),
        },
        ["count.4"] = new[]
        {
            L(Who.Pomo, Mood.Shock, "story.count.4.1", "Das war der letzte! Alle vier! Komm sofort heim!"),
        },
        ["talk.none"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.talk.none.1", "Die Reisekarte steht gleich da drüben. Vier Splitter, Krümel. Tick, tack."),
        },
        ["talk.some"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.talk.some.1", "Noch {0} Splitter. Und lass dir unterwegs nicht die Krümel abnehmen."),
        },
        ["talk.ready"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.talk.ready.1", "Der Vulkan ist offen. Ich bin bereit, wenn du's bist."),
        },
        ["talk.done1"] = new[]
        {
            L(Who.Pomo, Mood.Happy, "story.talk.done1.1", "Endlos-Modus? Ich zieh dich auf, wann immer du willst."),
        },
        ["talk.done2"] = new[]
        {
            L(Who.Pomo, Mood.Happy, "story.talk.done2.1", "Manchmal klingel ich einfach so. Aus Freude. Erschrick nicht."),
        },
        ["talk.done3"] = new[]
        {
            L(Who.Pomo, Mood.Normal, "story.talk.done3.1", "Weißt du, was das Schönste an einer Uhr ist? Dass sie weitergeht."),
        },
    };

    private static readonly Dictionary<string, Line> Barks = new Dictionary<string, Line>
    {
        ["start.World1"] = L(Who.Pomo, Mood.Normal, "story.bark.start.World1", "Tick, tack. Zwanzig Minuten, Krümel. Dann kommt der König."),
        ["start.World2"] = L(Who.Pomo, Mood.Normal, "story.bark.start.World2", "Riechst du das? Angebrannter Rosmarin. Beeil dich."),
        ["start.World5"] = L(Who.Pomo, Mood.Normal, "story.bark.start.World5", "Brrr. Zieh dir was Warmes an. Ach was – lauf einfach schneller."),
        ["start.World6"] = L(Who.Pomo, Mood.Normal, "story.bark.start.World6", "Bleib im Licht, Krümel. Ich mein's ernst."),
        ["start.World4"] = L(Who.Pomo, Mood.Normal, "story.bark.start.World4", "Zwanzig Minuten. Ich zähl sie alle. Versprochen."),
        ["boss.World1"] = L(Who.KeksKoenig, Mood.Normal, "story.bark.boss.World1", "AUF DIE KNIE! Ich bin dein <shake>KÖNIG</shake>!"),
        ["boss.World2"] = L(Who.Glutwurz, Mood.Normal, "story.bark.boss.World2", "Es brennt... alles brennt... <shake>GEH WEG VON MEINEN WURZELN!</shake>"),
        ["boss.World5"] = L(Who.Eiskaiser, Mood.Normal, "story.bark.boss.World5", "NIEMAND BETRITT MEIN EIS! <shake>ICH HALTE ES ZUSAMMEN!</shake>"),
        ["boss.World6"] = L(Who.Gespenst, Mood.Normal, "story.bark.boss.World6", "Buuuh. Du bist der Krümel von der Uhr, oder? <wave>Ich weiß, was sie getan hat.</wave>"),
        ["boss.World4"] = L(Who.Verkohlter, Mood.Normal, "story.bark.boss.World4", "Ich HÖRE dich ticken, Uhr. <shake>Hundert Jahre zu spät!</shake>"),
    };

    private static readonly Dictionary<string, string> Texts = new Dictionary<string, string>
    {
        ["story.cut.intro.1"] = "Seit die Großen fort sind, gehört die Küche uns: Arbeitsplatte, Kräuterwald, Gefrierfach und die dunkle Speisekammer.",
        ["story.cut.intro.2"] = "Und tief unter allem, im Vulkan, schläft der Große Ofen.",
        ["story.cut.intro.3"] = "Vor hundert Jahren rutschte dort ein Keks hinter das Blech. Niemand holte ihn heraus.",
        ["story.cut.intro.4"] = "Er glühte. Hundert Jahre lang. Bis aus ihm etwas anderes wurde.",
        ["story.cut.intro.5"] = "Jetzt heizt er den Ofen an, bis alles so schwarz ist wie er. Sein glühendes Herz brach er in vier Splitter – für vier Herrscher.",
        ["story.cut.intro.6"] = "Nur eine alte Küchenuhr tickte noch. Und sie weckte den mutigsten Krümel, den sie finden konnte.",
        ["story.cut.villain"] = "DER VERKOHLTE",
        ["story.cut.finale.1"] = "Das Herz war zerbrochen. Und der Große Ofen wurde still.",
        ["story.cut.finale.2"] = "Zum ersten Mal seit hundert Jahren war ein Keks fertig gebacken.",
        ["story.cut.ring"] = "DRRRRRING!",
        ["story.cut.end"] = "ENDE",
        ["story.cut.thanks"] = "Danke fürs Spielen!",
        ["story.cut.endless"] = "Der Endlos-Modus wartet auf dich.",
        ["story.name.pomo"] = "Pomo",
        ["story.name.squiddy"] = "Squiddy",
        ["story.ui.skip"] = "ESC  Überspringen",
        ["story.ui.next"] = "Weiter",
        ["story.ui.shard"] = "GLUTSPLITTER",
        ["story.ui.shardcount"] = "{0} von 4",
        ["story.ui.volcano"] = "DER VULKAN IST OFFEN",
        ["story.ui.volcanosub"] = "Story: Vulkan freigeschaltet",
        ["hub.prompt.pomo"] = "[E] Mit Pomo reden",
    };

    /// <summary>Ein Gespraech, null wenn es das nicht gibt.</summary>
    public static Line[] Convo(string id) => id != null && Convos.TryGetValue(id, out Line[] l) ? l : null;

    /// <summary>Ein Zwischenruf, null wenn es keinen gibt.</summary>
    public static Line? Bark(string id) => id != null && Barks.TryGetValue(id, out Line l) ? l : (Line?)null;

    /// <summary>Bildunterschrift oder UI-Text der Story.</summary>
    public static string T(string key) => Loc.Get(key, Texts.TryGetValue(key, out string de) ? de : key);
}
