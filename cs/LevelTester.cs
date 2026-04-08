using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AbiturEliteCode.cs;
// note: in the RunTests method (in MainWindow.axaml.cs) the handholding with the error explanaitions should decrease the further the user progress to make them do more on their own, if we introduce something (moderate) handholding it accepted, but on the last level of a section (which is always a mini-test) we want to check whether the user can do what they were taught on their own

public class TestResult
{
    public bool Success { get; set; }
    public string Feedback { get; set; }
    public Exception Error { get; set; }
}

public static class LevelTester
{
    public static TestResult Run(int levelId, Assembly assembly, string sourceCode = "")
    {
        try
        {
            string feedback = "";
            bool success = false;

            success = levelId switch
            {
                1 => TestLevel1(assembly, out feedback),
                2 => TestLevel2(assembly, out feedback),
                3 => TestLevel3(assembly, out feedback),
                4 => TestLevel4(assembly, out feedback),
                5 => TestLevel5(assembly, out feedback),
                6 => TestLevel6(assembly, out feedback),
                7 => TestLevel7(assembly, out feedback),
                8 => TestLevel8(assembly, sourceCode, out feedback),
                9 => TestLevel9(assembly, out feedback),
                10 => TestLevel10(assembly, out feedback),
                11 => TestLevel11(assembly, out feedback),
                12 => TestLevel12(assembly, out feedback),
                13 => TestLevel13(assembly, out feedback),
                14 => TestLevel14(assembly, out feedback),
                15 => TestLevel15(assembly, out feedback),
                16 => TestLevel16(assembly, out feedback),
                17 => TestLevel17(assembly, sourceCode, out feedback),
                18 => TestLevel18(assembly, sourceCode, out feedback),
                19 => TestLevel19(assembly, out feedback),
                20 => TestLevel20(assembly, out feedback),
                21 => TestLevel21(assembly, out feedback),
                22 => TestLevel22(assembly, sourceCode, out feedback),
                23 => TestLevel23(assembly, sourceCode, out feedback),
                24 => TestLevel24(assembly, sourceCode, out feedback),
                25 => TestLevel25(assembly, sourceCode, out feedback),
                26 => TestLevel26(assembly, sourceCode, out feedback),
                _ => throw new Exception($"Keine Tests für Level {levelId} definiert.")
            };
            return new TestResult { Success = success, Feedback = feedback };
        }
        catch (Exception ex)
        {
            return new TestResult { Success = false, Error = ex };
        }
    }

    private static bool TestLevel1(Assembly assembly, out string feedback)
    {
        Type tierType = assembly.GetType("Tier");
        if (tierType == null)
            throw new Exception(
                "Klasse 'Tier' nicht gefunden. Stelle sicher, dass du 'public class Tier' geschrieben hast.");

        ConstructorInfo ctor = tierType.GetConstructor(new[] { typeof(string), typeof(int) });
        if (ctor == null)
            throw new Exception(
                "Konstruktor Tier(string, int) fehlt. Füge einen Konstruktor mit zwei Parametern hinzu: public Tier(string name, int alter)");

        object tier = ctor.Invoke(new object[] { "Löwe", 5 });
        FieldInfo fName = tierType.GetField("name", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fAlter = tierType.GetField("alter", BindingFlags.NonPublic | BindingFlags.Instance);

        if (fName == null)
            throw new Exception("Feld 'name' fehlt oder ist nicht private. Füge hinzu: private string name;");
        if (fAlter == null)
            throw new Exception("Feld 'alter' fehlt oder ist nicht private. Füge hinzu: private int alter;");

        string actualName = (string)fName.GetValue(tier);
        int actualAlter = (int)fAlter.GetValue(tier);

        if (actualName == "Löwe" && actualAlter == 5)
        {
            feedback = "Klasse Tier korrekt implementiert! Felder und Konstruktor funktionieren.";
            return true;
        }

        throw new Exception(
            "Konstruktor setzt die Werte nicht korrekt. Im Konstruktor: this.name = name; und this.alter = alter;");
    }

    private static bool TestLevel2(Assembly assembly, out string feedback)
    {
        Type t = assembly.GetType("Tier");
        if (t == null) throw new Exception("Klasse 'Tier' nicht gefunden. Hast du sie gelöscht?");

        object obj = Activator.CreateInstance(t);

        MethodInfo mSet = t.GetMethod("SetAlter");
        if (mSet == null) mSet = t.GetMethod("setAlter");

        MethodInfo mGet = t.GetMethod("GetAlter");
        if (mGet == null) mGet = t.GetMethod("getAlter");

        if (mSet == null) throw new Exception("Methode SetAlter fehlt. Erstelle: public void SetAlter(int neuesAlter)");
        if (mGet == null) throw new Exception("Methode GetAlter fehlt. Erstelle: public int GetAlter()");

        FieldInfo fAlter = t.GetField("alter", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fAlter == null) throw new Exception("Feld 'alter' fehlt oder ist nicht private.");

        // initial value check
        fAlter.SetValue(obj, 10);

        // test 1: invalid (lower)
        mSet.Invoke(obj, new object[] { 5 });
        int val1 = (int)mGet.Invoke(obj, null);
        if (val1 != 10)
            throw new Exception(
                "Fehler: Alter wurde trotz kleinerem Wert geändert! SetAlter muss prüfen: if (neuesAlter > alter)");

        // test 2: valid (higher)
        mSet.Invoke(obj, new object[] { 12 });
        int val2 = (int)mGet.Invoke(obj, null);
        if (val2 != 12)
            throw new Exception(
                "Fehler: Alter wurde trotz gültigem Wert nicht geändert. Setze alter = neuesAlter wenn die Bedingung erfüllt ist.");

        feedback = "Kapselung und Validierung erfolgreich implementiert!";
        return true;
    }

    private static bool TestLevel3(Assembly assembly, out string feedback)
    {
        Type tTier = assembly.GetType("Tier");
        Type tLoewe = assembly.GetType("Loewe");

        if (tTier == null) throw new Exception("Klasse Tier fehlt. Erstelle: public abstract class Tier");
        if (tLoewe == null) throw new Exception("Klasse Loewe fehlt. Erstelle: public class Loewe : Tier");

        if (!tTier.IsAbstract)
            throw new Exception("Klasse Tier muss 'abstract' sein. Schreibe: public abstract class Tier");
        if (!tLoewe.IsSubclassOf(tTier))
            throw new Exception("Loewe erbt nicht von Tier. Füge hinzu: public class Loewe : Tier");

        FieldInfo fName = tTier.GetField("name", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fName == null)
            throw new Exception(
                "Feld 'name' fehlt in Tier. Laut Diagramm muss es geschützt sein: protected string name;");
        if (!fName.IsFamily)
            throw new Exception(
                "Feld 'name' in Tier muss 'protected' sein (#), nicht private oder public. Ändere zu: protected string name;");

        FieldInfo fLaenge = tLoewe.GetField("laenge", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fLaenge == null) throw new Exception("Feld 'laenge' fehlt in Loewe. Füge hinzu: private int laenge;");
        if (!fLaenge.IsPrivate)
            throw new Exception("Feld 'laenge' in Loewe muss 'private' sein. Ändere zu: private int laenge;");

        // check constructor
        ConstructorInfo ctor = tLoewe.GetConstructor(new[] { typeof(string), typeof(int) });
        if (ctor == null)
            throw new Exception(
                "Konstruktor Loewe(string, int) fehlt. Erstelle: public Loewe(string name, int laenge) : base(name)");

        // we cannot instantiate Tier, but we can instantiate Loewe
        object leo = ctor.Invoke(new object[] { "Simba", 50 });

        string actualName = (string)fName.GetValue(leo);
        if (actualName != "Simba")
            throw new Exception(
                "Der Konstruktor von Tier setzt 'name' nicht. Füge im Konstruktor von Tier hinzu: this.name = name; (und stelle sicher, dass Loewe base(name) aufruft)");

        int actualLaenge = (int)fLaenge.GetValue(leo);
        if (actualLaenge != 50)
            throw new Exception(
                "Der Konstruktor von Loewe setzt 'laenge' nicht. Füge im Konstruktor von Loewe hinzu: this.laenge = laenge;");

        MethodInfo mB = tLoewe.GetMethod("Bruellen");
        if (mB == null) mB = tLoewe.GetMethod("bruellen");

        if (mB == null) throw new Exception("Methode Bruellen fehlt. Erstelle: public string Bruellen()");

        string sound = (string)mB.Invoke(leo, null);
        if (string.IsNullOrEmpty(sound))
            throw new Exception(
                "Bruellen gibt nichts zurück. Die Methode sollte einen nicht-leeren String zurückgeben.");

        feedback = "Vererbung und Abstraktion korrekt implementiert!";
        return true;
    }

    private static bool TestLevel4(Assembly assembly, out string feedback)
    {
        Type tG = assembly.GetType("Gehege");
        Type tTier = assembly.GetType("Tier");

        if (tG == null) throw new Exception("Klasse 'Gehege' nicht gefunden.");
        if (tTier == null) throw new Exception("Klasse 'Tier' nicht gefunden.");

        // check if Tier is abstract
        if (tTier.IsAbstract) throw new Exception("Für dieses Level muss Klasse 'Tier' konkret (nicht abstract) sein.");

        // create Gehege instance
        object g;
        try
        {
            g = Activator.CreateInstance(tG);
        }
        catch
        {
            throw new Exception("Gehege konnte nicht instanziiert werden. Prüfe den Konstruktor.");
        }

        if (g == null) throw new Exception("Gehege-Instanz ist null.");

        // find methods
        MethodInfo mAdd = tG.GetMethod("Hinzufuegen");
        if (mAdd == null) mAdd = tG.GetMethod("hinzufuegen");

        MethodInfo mCount = tG.GetMethod("AnzahlTiere");
        if (mCount == null) mCount = tG.GetMethod("anzahlTiere");

        if (mAdd == null) throw new Exception("Methode 'Hinzufuegen' nicht gefunden.");
        if (mCount == null) throw new Exception("Methode 'AnzahlTiere' nicht gefunden.");

        // check method signatures
        var addParams = mAdd.GetParameters();
        if (addParams.Length != 1 || addParams[0].ParameterType != tTier)
            throw new Exception("Methode Hinzufuegen muss Parameter vom Typ Tier haben.");

        if (mCount.ReturnType != typeof(int)) throw new Exception("Methode AnzahlTiere muss int zurückgeben.");

        // create Tier instance
        object animal;
        try
        {
            animal = Activator.CreateInstance(tTier);
        }
        catch
        {
            throw new Exception(
                "Tier konnte nicht instanziiert werden. Tier braucht einen Konstruktor ohne Parameter.");
        }

        if (animal == null) throw new Exception("Tier-Instanz ist null.");

        // test initial count
        int initialCount;
        try
        {
            initialCount = (int)mCount.Invoke(g, null);
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler bei AnzahlTiere: {ex.Message}");
        }

        if (initialCount != 0)
            throw new Exception(
                $"AnzahlTiere sollte initial 0 sein, ist aber {initialCount}. Initialisiere die Liste im Konstruktor.");

        // add one animal
        try
        {
            mAdd.Invoke(g, new[] { animal });
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler bei Hinzufuegen: {ex.Message}");
        }

        // test count after adding
        int countAfterAdd;
        try
        {
            countAfterAdd = (int)mCount.Invoke(g, null);
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler bei AnzahlTiere: {ex.Message}");
        }

        if (countAfterAdd == 1)
        {
            feedback = "Gehege und List<Tier> korrekt implementiert!";
            return true;
        }

        throw new Exception($"AnzahlTiere gibt {countAfterAdd} statt 1 zurück. Prüfe die Liste.");
    }

    private static bool TestLevel5(Assembly assembly, out string feedback)
    {
        Type tG = assembly.GetType("Gehege");
        if (tG == null) throw new Exception("Klasse 'Gehege' fehlt.");

        Type tT = assembly.GetType("Tier");
        if (tT == null) throw new Exception("Klasse 'Tier' fehlt.");

        object g = Activator.CreateInstance(tG);

        object CreateTier(int age)
        {
            return Activator.CreateInstance(tT, age);
        }

        object t1 = CreateTier(5);
        object t2 = CreateTier(20);
        object t3 = CreateTier(10);

        FieldInfo fList = tG.GetField("bewohner", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (fList == null) throw new Exception("Feld 'bewohner' fehlt.");

        var listInstance = fList.GetValue(g);

        if (listInstance == null)
        {
            Type listType = typeof(List<>).MakeGenericType(tT);
            listInstance = Activator.CreateInstance(listType);
            fList.SetValue(g, listInstance);
        }

        MethodInfo listAdd = listInstance.GetType().GetMethod("Add");

        listAdd.Invoke(listInstance, new[] { t1 });
        listAdd.Invoke(listInstance, new[] { t2 });
        listAdd.Invoke(listInstance, new[] { t3 });

        MethodInfo mAlgo = tG.GetMethod("ErmittleAeltestes");
        if (mAlgo == null) mAlgo = tG.GetMethod("ermittleAeltestes");

        if (mAlgo == null) throw new Exception("Methode 'ErmittleAeltestes' fehlt.");
        if (mAlgo.ReturnType != tT) throw new Exception("Rückgabetyp muss 'Tier' sein.");

        object result = mAlgo.Invoke(g, null);

        if (result == null) throw new Exception("Methode 'ErmittleAeltestes' hat 'null' zurückgegeben.");

        MethodInfo mGetAlter = tT.GetMethod("GetAlter");
        if (mGetAlter == null) mGetAlter = tT.GetMethod("getAlter");

        if (mGetAlter == null) throw new Exception("Methode 'GetAlter()' fehlt.");

        int resultAge = (int)mGetAlter.Invoke(result, null);

        if (resultAge == 20)
        {
            feedback = "Mini-Prüfung bestanden! Algorithmus korrekt.";
            return true;
        }

        throw new Exception($"Falsches Tier zurückgegeben (Alter: {resultAge}, erwartet: 20).");
    }

    private static object SafeCreatePaket(Type tPaket, string ort, double gewicht)
    {
        // try finding correct constructor: (string, double)
        ConstructorInfo ctorCorrect = tPaket.GetConstructor(new[] { typeof(string), typeof(double) });
        if (ctorCorrect != null) return ctorCorrect.Invoke(new object[] { ort, gewicht });

        // try finding reversed constructor: (double, string) -> common mistake
        ConstructorInfo ctorReversed = tPaket.GetConstructor(new[] { typeof(double), typeof(string) });
        if (ctorReversed != null)
            throw new Exception(
                "Fehler im Konstruktor von 'Paket': Die Reihenfolge der Parameter ist falsch.\nErwartet: Paket(string ziel, double gewicht)\nGefunden: Paket(double gewicht, string ziel)\nBitte passen Sie die Reihenfolge an das Diagramm an.");

        throw new Exception(
            "Konstruktor für 'Paket' nicht gefunden. Erwartet: public Paket(string ziel, double gewicht).");
    }

    private static bool TestLevel6(Assembly assembly, out string feedback)
    {
        Type tPaket = assembly.GetType("Paket");
        if (tPaket == null) throw new Exception("Klasse 'Paket' nicht gefunden.");

        Type tLager = assembly.GetType("Lager");
        if (tLager == null) throw new Exception("Klasse 'Lager' nicht gefunden.");

        MethodInfo mAdd = tLager.GetMethod("Hinzufuegen") ?? tLager.GetMethod("hinzufuegen");
        if (mAdd == null) throw new Exception("Methode 'Hinzufuegen' fehlt in der Klasse Lager.");

        MethodInfo mErmittle = tLager.GetMethod("ErmittleLeichtestes") ?? tLager.GetMethod("ermittleLeichtestes");
        if (mErmittle == null)
        {
            if (tLager.GetMethod("ErmittleSchwerstes") != null || tLager.GetMethod("ermittleSchwerstes") != null)
                throw new Exception(
                    "Achtung: Die Aufgabe wurde geändert! Wir suchen nun das *leichteste* Paket. Bitte benennen Sie die Methode 'ErmittleLeichtestes'.");

            throw new Exception("Methode 'ErmittleLeichtestes' fehlt in der Klasse Lager.");
        }

        object lager = Activator.CreateInstance(tLager);

        // test case 1: empty list
        object resultEmpty = mErmittle.Invoke(lager, null);
        if (resultEmpty != null)
            throw new Exception("ErmittleLeichtestes() muss 'null' zurückgeben, wenn das Lager leer ist.");

        // test case 2: filled list
        try
        {
            mAdd.Invoke(lager, new[] { SafeCreatePaket(tPaket, "Berlin", 50.0) });
            mAdd.Invoke(lager, new[] { SafeCreatePaket(tPaket, "Hamburg", 5.5) }); // The lightest
            mAdd.Invoke(lager, new[] { SafeCreatePaket(tPaket, "München", 10.0) });
        }
        catch (TargetInvocationException ex)
        {
            throw new Exception($"Fehler beim Hinzufügen von Paketen: {ex.InnerException?.Message ?? ex.Message}");
        }

        object result = mErmittle.Invoke(lager, null);

        if (result == null) throw new Exception("ErmittleLeichtestes() liefert 'null', obwohl Pakete im Lager sind.");

        MethodInfo mGetW = tPaket.GetMethod("GetGewicht") ?? tPaket.GetMethod("getGewicht");
        if (mGetW == null) throw new Exception("Methode 'GetGewicht()' fehlt in Klasse Paket.");

        double resWeight = (double)mGetW.Invoke(result, null);

        if (Math.Abs(resWeight - 5.5) < 0.01)
        {
            feedback = "Algorithmus korrekt implementiert! Das leichteste Paket wurde gefunden.";
            return true;
        }

        throw new Exception(
            $"Falsches Paket ermittelt. Gewicht des zurückgegebenen Pakets: {resWeight}, Erwartet: 5.5 (das Leichteste).");
    }

    private static bool TestLevel7(Assembly assembly, out string feedback)
    {
        Type tLager = assembly.GetType("Lager");
        Type tPaket = assembly.GetType("Paket");
        if (tLager == null || tPaket == null) throw new Exception("Klasse Lager oder Paket fehlt.");

        object lager = Activator.CreateInstance(tLager);

        MethodInfo mAdd = tLager.GetMethod("Hinzufuegen") ?? tLager.GetMethod("hinzufuegen");
        MethodInfo mFilter = tLager.GetMethod("FilterePakete") ?? tLager.GetMethod("filterePakete");

        if (mAdd == null) throw new Exception("Methode Hinzufuegen fehlt.");
        if (mFilter == null) throw new Exception("Methode FilterePakete fehlt.");

        mAdd.Invoke(lager, new[] { SafeCreatePaket(tPaket, "Berlin", 15.0) }); // match
        mAdd.Invoke(lager, new[] { SafeCreatePaket(tPaket, "München", 20.0) }); // wrong city
        mAdd.Invoke(lager, new[] { SafeCreatePaket(tPaket, "Berlin", 5.0) }); // too light
        mAdd.Invoke(lager, new[] { SafeCreatePaket(tPaket, "Berlin", 10.0) }); // boundary (not > 10)

        object resObj = mFilter.Invoke(lager, new object[] { "Berlin" });

        IList list = resObj as IList;
        if (list == null) throw new Exception("Rückgabewert ist keine Liste (List<Paket>).");

        if (list.Count == 1)
        {
            feedback = "Filterung erfolgreich! Nur Pakete > 10kg und passender Ort wurden übernommen.";
            return true;
        }

        throw new Exception(
            $"Falsche Anzahl Pakete zurückgegeben. Erwartet: 1, Erhalten: {list.Count}. Prüfen Sie die Bedingungen (ort == ziel && gewicht > 10).");
    }

    private static bool TestLevel8(Assembly assembly, string sourceCode, out string feedback)
    {
        if (sourceCode.Contains(".Sort(") || sourceCode.Contains(".OrderBy("))
            throw new Exception(
                "Anti-Cheat: Bitte verwenden Sie keine fertigen Sortierfunktionen wie .Sort() oder .OrderBy(), sondern implementieren Sie Bubble Sort selbst.");

        Type tLager = assembly.GetType("Lager");
        Type tPaket = assembly.GetType("Paket");
        if (tLager == null) throw new Exception("Klasse Lager fehlt.");

        object lager = Activator.CreateInstance(tLager);

        FieldInfo fPakete = tLager.GetField("pakete", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fPakete == null) throw new Exception("Feld 'pakete' (List<Paket>) fehlt oder ist nicht private.");

        Type listType = typeof(List<>).MakeGenericType(tPaket);
        IList list = (IList)Activator.CreateInstance(listType);

        list.Add(SafeCreatePaket(tPaket, "A", 100.0));
        list.Add(SafeCreatePaket(tPaket, "B", 10.0));
        list.Add(SafeCreatePaket(tPaket, "C", 50.0));
        fPakete.SetValue(lager, list);

        MethodInfo mSort = tLager.GetMethod("SortiereNachGewicht") ?? tLager.GetMethod("sortiereNachGewicht");
        if (mSort == null) throw new Exception("Methode SortiereNachGewicht fehlt.");

        mSort.Invoke(lager, null);

        IList sortedList = (IList)fPakete.GetValue(lager);
        MethodInfo mGetW = tPaket.GetMethod("GetGewicht") ?? tPaket.GetMethod("getGewicht");

        double w1 = (double)mGetW.Invoke(sortedList[0], null);
        double w2 = (double)mGetW.Invoke(sortedList[1], null);
        double w3 = (double)mGetW.Invoke(sortedList[2], null);

        if (w1 <= w2 && w2 <= w3)
        {
            feedback = "Bubble Sort erfolgreich implementiert! Die Pakete sind aufsteigend sortiert.";
            return true;
        }

        throw new Exception($"Sortierung inkorrekt. Reihenfolge: {w1}, {w2}, {w3}. Erwartet: 10, 50, 100.");
    }

    private static bool TestLevel9(Assembly assembly, out string feedback)
    {
        Type tKnoten = assembly.GetType("Waggon");
        Type tBand = assembly.GetType("Gueterzug");
        Type tPaket = assembly.GetType("Paket");

        if (tKnoten == null) throw new Exception("Klasse 'Waggon' fehlt. Hast du sie richtig benannt?");
        if (tBand == null) throw new Exception("Klasse 'Gueterzug' fehlt.");

        object band = Activator.CreateInstance(tBand);
        MethodInfo mAnh = tBand.GetMethod("Anhaengen") ?? tBand.GetMethod("anhaengen");
        if (mAnh == null) throw new Exception("Methode 'Anhaengen' fehlt.");

        object p1 = SafeCreatePaket(tPaket, "A", 10.0);
        object p2 = SafeCreatePaket(tPaket, "B", 20.0);

        // add 1
        mAnh.Invoke(band, new[] { p1 });

        FieldInfo fKopf = tBand.GetField("lokomotive", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fKopf == null) throw new Exception("Feld 'lokomotive' in Gueterzug fehlt.");

        object kopfNode = fKopf.GetValue(band);
        if (kopfNode == null)
            throw new Exception(
                "Die Lokomotive ist null nach dem ersten Einfügen. Hast du den 1. Fall vergessen (oder das 'return;' dort)?");

        // check content of head
        FieldInfo fInhalt = tKnoten.GetField("ladung", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fInhalt == null) throw new Exception("Feld 'ladung' in Waggon fehlt.");
        if (fInhalt.GetValue(kopfNode) != p1) throw new Exception("Erster Waggon enthält nicht das korrekte Paket.");

        // add 2
        mAnh.Invoke(band, new[] { p2 });

        // check linking
        FieldInfo fNachfolger = tKnoten.GetField("naechster", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fNachfolger == null) throw new Exception("Feld 'naechster' in Waggon fehlt.");

        object secondNode = fNachfolger.GetValue(kopfNode);
        if (secondNode == null)
            throw new Exception(
                "Verkettung fehlerhaft. 'naechster' von der Lokomotive ist null. Die while-Schleife hat nicht richtig angehängt.");

        if (fInhalt.GetValue(secondNode) != p2) throw new Exception("Zweiter Waggon enthält falsches Paket.");

        feedback = "Verkettete Liste (Güterzug) funktioniert perfekt! Du hast das Prinzip verstanden.";
        return true;
    }

    private static bool TestLevel10(Assembly assembly, out string feedback)
    {
        Type tLogistik = assembly.GetType("LogistikZentrum");
        Type tPaket = assembly.GetType("Paket");
        if (tLogistik == null) throw new Exception("Klasse LogistikZentrum fehlt.");

        object zentrum = Activator.CreateInstance(tLogistik);
        FieldInfo fList = tLogistik.GetField("allePakete", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fList == null) throw new Exception("Feld 'allePakete' fehlt.");

        IList list = fList.GetValue(zentrum) as IList;

        list.Add(SafeCreatePaket(tPaket, "Berlin", 10.0));
        list.Add(SafeCreatePaket(tPaket, "Berlin", 90.0)); // 2nd
        list.Add(SafeCreatePaket(tPaket, "München", 99.0));
        list.Add(SafeCreatePaket(tPaket, "Berlin", 100.0)); // 1st
        list.Add(SafeCreatePaket(tPaket, "Berlin", 5.0));
        list.Add(SafeCreatePaket(tPaket, "Berlin", 50.0)); // 3rd

        MethodInfo mTop3 = tLogistik.GetMethod("GetTop3Schwere") ?? tLogistik.GetMethod("getTop3Schwere");
        if (mTop3 == null) throw new Exception("Methode GetTop3Schwere fehlt.");

        object resObj = mTop3.Invoke(zentrum, new object[] { "Berlin" });
        IList resList = resObj as IList;

        if (resList == null) throw new Exception("Keine Liste zurückgegeben.");
        if (resList.Count != 3)
            throw new Exception($"Liste sollte genau 3 Elemente enthalten, hat aber {resList.Count}.");

        MethodInfo mGetW = tPaket.GetMethod("GetGewicht") ?? tPaket.GetMethod("getGewicht");
        double w1 = (double)mGetW.Invoke(resList[0], null);
        double w2 = (double)mGetW.Invoke(resList[1], null);
        double w3 = (double)mGetW.Invoke(resList[2], null);

        // expecting: 100, 90, 50
        if (Math.Abs(w1 - 100.0) < 0.1 && Math.Abs(w2 - 90.0) < 0.1 && Math.Abs(w3 - 50.0) < 0.1)
        {
            feedback = "Mini-Exam bestanden! Filterung und Sortierung korrekt.";
            return true;
        }

        throw new Exception($"Falsche Reihenfolge oder Pakete. Erhalten: {w1}, {w2}, {w3}. Erwartet: 100, 90, 50.");
    }

    private static bool TestLevel11(Assembly assembly, out string feedback)
    {
        Type tSchueler = assembly.GetType("Schueler");
        Type tKlasse = assembly.GetType("Klasse");

        if (tSchueler == null) throw new Exception("Klasse 'Schueler' fehlt.");
        if (tKlasse == null) throw new Exception("Klasse 'Klasse' fehlt.");

        // check constructors
        ConstructorInfo ctorSchueler = tSchueler.GetConstructor(new[] { typeof(int) });
        if (ctorSchueler == null) throw new Exception("Konstruktor Schueler(int note) fehlt.");

        ConstructorInfo ctorKlasse = tKlasse.GetConstructor(new[] { typeof(string) });
        if (ctorKlasse == null) throw new Exception("Konstruktor Klasse(string bezeichnung) fehlt.");

        // setup objects
        object klasseObj = ctorKlasse.Invoke(new object[] { "13-A" });

        // verify list initialization in constructor
        FieldInfo fListe = tKlasse.GetField("schuelerListe", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fListe == null)
            throw new Exception("Feld 'schuelerListe' fehlt in Klasse 'Klasse' oder ist nicht private.");
        if (fListe.GetValue(klasseObj) == null)
            throw new Exception(
                "Die Liste 'schuelerListe' ist null. Sie muss im Konstruktor mit 'new List<Schueler>()' initialisiert werden.");

        MethodInfo mAdd = tKlasse.GetMethod("AddSchueler") ?? tKlasse.GetMethod("addSchueler");
        if (mAdd == null) mAdd = tKlasse.GetMethod("addSchueler");
        if (mAdd == null) throw new Exception("Methode 'AddSchueler' fehlt.");

        MethodInfo mSchnitt = tKlasse.GetMethod("BerechneSchnittBestanden") ??
                              tKlasse.GetMethod("berechneSchnittBestanden");
        if (mSchnitt == null) throw new Exception("Methode 'BerechneSchnittBestanden' fehlt.");

        object CreateSchueler(int n)
        {
            return ctorSchueler.Invoke(new object[] { n });
        }

        // logic test
        // scenario: 
        // student a: 15 points (passed)
        // student b: 10 points (passed)
        // student c: 3 points (failed -> should be ignored)
        // student d: 5 points (passed)
        // calculation: (15 + 10 + 5) / 3 = 10.0d
        mAdd.Invoke(klasseObj, new[] { CreateSchueler(15) });
        mAdd.Invoke(klasseObj, new[] { CreateSchueler(10) });
        mAdd.Invoke(klasseObj, new[] { CreateSchueler(3) });
        mAdd.Invoke(klasseObj, new[] { CreateSchueler(5) });

        double result = (double)mSchnitt.Invoke(klasseObj, null);

        if (Math.Abs(result - 10.0) < 0.01)
        {
            // test edge case: only failures
            object kFail = ctorKlasse.Invoke(new object[] { "Fail-Class" });
            mAdd.Invoke(kFail, new[] { CreateSchueler(2) });
            double resFail = (double)mSchnitt.Invoke(kFail, null);

            if (Math.Abs(resFail) < 0.01)
            {
                feedback = "Sehr gut! Der Schnitt wurde korrekt berechnet und nicht bestandene Schüler ignoriert.";
                return true;
            }

            throw new Exception(
                "Wenn keine Schüler bestanden haben, muss 0.0 zurückgegeben werden (Division durch Null verhindern!).");
        }

        // diagnose error
        if (Math.Abs(result - 8.25) < 0.01)
            throw new Exception("Fehler: Sie haben alle Schüler (auch die mit Note < 5) mit einberechnet.");
        throw new Exception(
            $"Falsches Ergebnis. Erwartet: 10.0. Erhalten: {result:F2}. Prüfen Sie die Bedingung (Note > 4).");
    }

    private static bool TestLevel12(Assembly assembly, out string feedback)
    {
        Type tSchule = assembly.GetType("Schule");
        Type tLehrer = assembly.GetType("Lehrer");
        Type tKlasse = assembly.GetType("Klasse");

        if (tSchule == null || tLehrer == null || tKlasse == null)
            throw new Exception("Eine der Klassen (Schule, Lehrer, Klasse) fehlt.");

        object schule = Activator.CreateInstance(tSchule);
        object k1 = Activator.CreateInstance(tKlasse); // dummy classes
        object k2 = Activator.CreateInstance(tKlasse);
        object k3 = Activator.CreateInstance(tKlasse);

        object l1 = Activator.CreateInstance(tLehrer);
        object l2 = Activator.CreateInstance(tLehrer);

        // setup methods
        MethodInfo mAddLehrer = tSchule.GetMethod("AddLehrer") ?? tSchule.GetMethod("addLehrer");
        MethodInfo mFind = tSchule.GetMethod("FindeVielBeschaeftigte") ?? tSchule.GetMethod("findeVielBeschaeftigte");
        MethodInfo mAddKlasse = tLehrer.GetMethod("AddKlasse") ?? tLehrer.GetMethod("addKlasse");

        if (mAddLehrer == null) throw new Exception("Methode AddLehrer in Schule fehlt.");
        if (mFind == null) throw new Exception("Methode FindeVielBeschaeftigte in Schule fehlt.");
        if (mAddKlasse == null) throw new Exception("Methode AddKlasse in Lehrer fehlt.");

        // scene:
        // lehrer 1 has 3 classes (should be found)
        mAddKlasse.Invoke(l1, new[] { k1 });
        mAddKlasse.Invoke(l1, new[] { k2 });
        mAddKlasse.Invoke(l1, new[] { k3 });

        // lehrer 2 has 2 classes (shouldnt be found, strict > 2)
        mAddKlasse.Invoke(l2, new[] { k1 });
        mAddKlasse.Invoke(l2, new[] { k2 });

        mAddLehrer.Invoke(schule, new[] { l1 });
        mAddLehrer.Invoke(schule, new[] { l2 });

        object resultObj = mFind.Invoke(schule, null);
        IList resultList = resultObj as IList;

        if (resultList == null) throw new Exception("Rückgabewert ist keine Liste.");

        if (resultList.Count == 1)
        {
            if (resultList[0] == l1)
            {
                feedback = "Korrekte Filterung! Überlastete Lehrer wurden identifiziert.";
                return true;
            }

            throw new Exception("Liste hat richtige Länge, aber falschen Inhalt.");
        }

        if (resultList.Count == 2)
            throw new Exception(
                "Es wurden zu viele Lehrer gefunden. Die Bedingung war 'mehr als 2' (> 2), nicht 'ab 2' (>= 2).");

        throw new Exception($"Falsche Anzahl Ergebnisse. Erwartet: 1, Erhalten: {resultList.Count}.");
    }

    private static bool TestLevel13(Assembly assembly, out string feedback)
    {
        Type tSchueler = assembly.GetType("Schueler");
        Type tFehltag = assembly.GetType("Fehltag");

        if (tSchueler == null || tFehltag == null)
            throw new Exception("Klassenstruktur unvollständig (Schueler oder Fehltag fehlt).");

        ConstructorInfo ctorFehltag = tFehltag.GetConstructor(new[] { typeof(DateTime), typeof(bool) });
        bool usesLocalDate = false;
        Type tLocalDate = assembly.GetType("LocalDate");

        if (ctorFehltag == null && tLocalDate != null)
        {
            ctorFehltag = tFehltag.GetConstructor(new[] { tLocalDate, typeof(bool) });
            if (ctorFehltag != null) usesLocalDate = true;
        }

        if (ctorFehltag == null)
            throw new Exception("Konstruktor Fehltag(DateTime, bool) oder Fehltag(LocalDate, bool) fehlt.");

        MethodInfo mAdd = tSchueler.GetMethod("AddFehltag") ?? tSchueler.GetMethod("addFehltag");
        if (mAdd == null) throw new Exception("Methode AddFehltag fehlt in Klasse Schueler.");

        MethodInfo mCheck = tSchueler.GetMethod("HatKritischGefehlt") ?? tSchueler.GetMethod("hatKritischGefehlt");
        if (mCheck == null) throw new Exception("Methode HatKritischGefehlt fehlt.");

        object s = Activator.CreateInstance(tSchueler);

        void AddDay(int daysAgo, bool exc)
        {
            object dateObj;
            if (usesLocalDate)
            {
                object nowObj = tLocalDate.GetMethod("Now").Invoke(null, null);
                dateObj = tLocalDate.GetMethod("PlusDays").Invoke(nowObj, new object[] { (long)-daysAgo });
            }
            else
            {
                dateObj = DateTime.Now.AddDays(-daysAgo);
            }

            object ft = ctorFehltag.Invoke(new[] { dateObj, exc });
            mAdd.Invoke(s, new[] { ft });
        }

        // case 1: no absences
        if ((bool)mCheck.Invoke(s, null)) throw new Exception("Gibt true zurück, obwohl keine Fehltage existieren.");

        // case 2: old unexcused absence (e.g. 40 days ago) -> should be false
        AddDay(40, false);
        if ((bool)mCheck.Invoke(s, null))
            throw new Exception("Gibt true zurück, obwohl der unentschuldigte Fehltag länger als 1 Monat zurückliegt.");

        // case 3: recent excused absence -> should still be false
        AddDay(5, true);
        if ((bool)mCheck.Invoke(s, null))
            throw new Exception("Gibt true zurück, obwohl der aktuelle Fehltag entschuldigt ist.");

        // case 4: recent unexcused absence -> should be true
        AddDay(10, false);
        if (!(bool)mCheck.Invoke(s, null))
            throw new Exception("Gibt false zurück, obwohl ein unentschuldigter Fehltag im letzten Monat existiert.");

        feedback = "Datumslogik korrekt implementiert! Zeitraum und Status wurden richtig geprüft.";
        return true;
    }

    private static bool TestLevel14(Assembly assembly, out string feedback)
    {
        Type tSchule = assembly.GetType("Schule");
        Type tKlasse = assembly.GetType("Klasse");
        Type tSchueler = assembly.GetType("Schueler");

        if (tSchule == null || tKlasse == null || tSchueler == null)
            throw new Exception("Klassenstruktur fehlt (Schule, Klasse oder Schueler).");

        object schule = Activator.CreateInstance(tSchule);

        // methods setup
        MethodInfo mAddKlasse = tSchule.GetMethod("AddKlasse") ?? tSchule.GetMethod("addKlasse");
        MethodInfo mAddSchueler = tKlasse.GetMethod("AddSchueler") ?? tKlasse.GetMethod("addSchueler");
        MethodInfo mWarn = tSchule.GetMethod("ErstelleWarnungen") ?? tSchule.GetMethod("erstelleWarnungen");

        if (mAddKlasse == null) throw new Exception("AddKlasse fehlt.");
        if (mAddSchueler == null) throw new Exception("AddSchueler fehlt.");
        if (mWarn == null) throw new Exception("ErstelleWarnungen fehlt.");

        // constructors
        ConstructorInfo cKlasse = tKlasse.GetConstructor(new[] { typeof(string) });
        ConstructorInfo cSchueler = tSchueler.GetConstructor(new[] { typeof(string), typeof(int) });

        if (cKlasse == null || cSchueler == null)
            throw new Exception("Konstruktoren fehlen (Klasse(string) oder Schueler(string, int)).");

        // build hierarchy
        // class 10a
        object k1 = cKlasse.Invoke(new object[] { "10A" });
        object s1 = cSchueler.Invoke(new object[] { "Max", 3 }); // fail
        object s2 = cSchueler.Invoke(new object[] { "Lisa", 10 }); // pass
        mAddSchueler.Invoke(k1, new[] { s1 });
        mAddSchueler.Invoke(k1, new[] { s2 });

        // class 11b
        object k2 = cKlasse.Invoke(new object[] { "11B" });
        object s3 = cSchueler.Invoke(new object[] { "Tom", 4 }); // fail
        mAddSchueler.Invoke(k2, new[] { s3 });

        mAddKlasse.Invoke(schule, new[] { k1 });
        mAddKlasse.Invoke(schule, new[] { k2 });

        string result = (string)mWarn.Invoke(schule, null);

        if (string.IsNullOrEmpty(result)) throw new Exception("Rückgabe ist leer.");

        bool hasMax = result.Contains("Max") && result.Contains("10A") && result.Contains("3");
        bool hasTom = result.Contains("Tom") && result.Contains("11B") && result.Contains("4");
        bool hasLisa = result.Contains("Lisa");

        if (hasMax && hasTom && !hasLisa)
        {
            feedback = "Sektion 3 erfolgreich abgeschlossen! Der Serienbrief wurde korrekt generiert.";
            return true;
        }

        if (hasLisa) throw new Exception("Fehler: Schüler mit Note >= 5 (Lisa) wurden auch in den Brief aufgenommen.");
        if (!hasMax || !hasTom) throw new Exception("Fehler: Nicht alle gefährdeten Schüler wurden gefunden.");

        throw new Exception("Formatierung des Strings entspricht nicht den Vorgaben.");
    }

    private static bool TestLevel15(Assembly assembly, out string feedback)
    {
        Type tRover = assembly.GetType("Rover");
        Type tZentrum = assembly.GetType("Kontrollzentrum");

        if (tRover == null) throw new Exception("Klasse 'Rover' fehlt.");
        if (tZentrum == null) throw new Exception("Klasse 'Kontrollzentrum' fehlt.");

        ConstructorInfo ctorRover = tRover.GetConstructor(new[] { typeof(string) });
        if (ctorRover == null) throw new Exception("Konstruktor Rover(string id) fehlt.");

        // check if id is set in constructor
        object roverIdTest = ctorRover.Invoke(new object[] { "TESTID" });
        FieldInfo fIdL15 = tRover.GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fIdL15 == null) throw new Exception("Feld 'id' in Rover fehlt.");
        if ((string)fIdL15.GetValue(roverIdTest) != "TESTID")
            throw new Exception("Konstruktor Rover(string id) setzt 'id' nicht. Füge hinzu: this.id = id;");

        ConstructorInfo ctorZentrum = tZentrum.GetConstructor(Type.EmptyTypes);
        if (ctorZentrum == null) throw new Exception("Standardkonstruktor für Kontrollzentrum fehlt.");

        object zentrum = ctorZentrum.Invoke(null);

        FieldInfo fRover = tZentrum.GetField("rover", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fRover == null) throw new Exception("Feld 'rover' in Kontrollzentrum fehlt oder ist nicht private.");

        object rover = fRover.GetValue(zentrum);
        if (rover == null)
            throw new Exception("Der Rover wurde im Konstruktor von Kontrollzentrum nicht initialisiert.");

        MethodInfo mVerarbeite = tZentrum.GetMethod("VerarbeiteKommando") ?? tZentrum.GetMethod("verarbeiteKommando");
        if (mVerarbeite == null) throw new Exception("Methode 'VerarbeiteKommando' im Kontrollzentrum fehlt.");

        // methods in rover
        MethodInfo mMove = tRover.GetMethod("Move") ?? tRover.GetMethod("move");
        MethodInfo mTurn = tRover.GetMethod("Turn") ?? tRover.GetMethod("turn");
        MethodInfo mScan = tRover.GetMethod("Scan") ?? tRover.GetMethod("scan");

        if (mMove == null) throw new Exception("Methode 'Move(int, int)' in Rover fehlt. Prüfen Sie die Parameter.");
        if (mTurn == null) throw new Exception("Methode 'Turn(string)' in Rover fehlt.");
        if (mScan == null) throw new Exception("Methode 'Scan(string)' in Rover fehlt.");

        // fields to verify functionality
        FieldInfo fId = tRover.GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fPos = tRover.GetField("position", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fAus = tRover.GetField("ausrichtung", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fLetzterScan = tZentrum.GetField("letzterScan", BindingFlags.NonPublic | BindingFlags.Instance);

        if (fId == null) throw new Exception("Feld 'id' in Rover fehlt.");
        if (fPos == null || fAus == null)
            throw new Exception("Die privaten Felder 'position' oder 'ausrichtung' in Rover fehlen.");
        if (fLetzterScan == null) throw new Exception("Feld 'letzterScan' in Kontrollzentrum fehlt.");

        // test execution 1: move
        try
        {
            mVerarbeite.Invoke(zentrum, new object[] { "MV;42;90" });
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler beim Verarbeiten von 'MV;42;90': {ex.InnerException?.Message ?? ex.Message}");
        }

        int[] actualPos = (int[])fPos.GetValue(rover);
        if (actualPos == null || actualPos.Length != 2 || actualPos[0] != 42 || actualPos[1] != 90)
            throw new Exception(
                "Das Kommando 'MV;42;90' wurde nicht korrekt an Move() übergeben. Prüfen Sie das int-Array.");

        // test execution 2: turn
        try
        {
            mVerarbeite.Invoke(zentrum, new object[] { "TR;LEFT" });
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler beim Verarbeiten von 'TR;LEFT': {ex.InnerException?.Message ?? ex.Message}");
        }

        string actualDir = (string)fAus.GetValue(rover);
        if (actualDir != "LEFT") throw new Exception("Das Kommando 'TR;LEFT' wurde nicht korrekt an Turn() übergeben.");

        // test execution 3: scan
        try
        {
            mVerarbeite.Invoke(zentrum, new object[] { "SC;ROCK" });
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler beim Verarbeiten von 'SC;ROCK': {ex.InnerException?.Message ?? ex.Message}");
        }

        // check both possible date implementations
        object actualScan = fLetzterScan.GetValue(zentrum);
        if (actualScan == null || (actualScan is DateTime dtScan && dtScan == default))
            throw new Exception(
                "Das Kommando 'SC;ROCK' hat 'letzterScan' nicht aktualisiert. Stellen Sie sicher, dass Sie den Rückgabewert der Scan-Methode speichern.");

        feedback =
            "Protokoll korrekt geparst! Switch-Anweisung, Array-Konvertierung und Datum wurden richtig verarbeitet.";
        return true;
    }

    private static bool TestLevel16(Assembly assembly, out string feedback)
    {
        Type tPaket = assembly.GetType("DatenPaket");
        Type tKnoten = assembly.GetType("NetzwerkKnoten");

        if (tPaket == null) throw new Exception("Klasse 'DatenPaket' fehlt.");
        if (tKnoten == null) throw new Exception("Klasse 'NetzwerkKnoten' fehlt.");

        ConstructorInfo ctorPaket = tPaket.GetConstructor(new[] { typeof(int[]) });
        if (ctorPaket == null) throw new Exception("Konstruktor DatenPaket(int[] daten) fehlt.");

        ConstructorInfo ctorKnoten = tKnoten.GetConstructor(new[] { typeof(string) });
        if (ctorKnoten == null) throw new Exception("Konstruktor NetzwerkKnoten(string id) fehlt.");

        object knoten = ctorKnoten.Invoke(new object[] { "Node-01" });

        // check if knotenId is set in constructor
        FieldInfo fKnotenId = tKnoten.GetField("knotenId", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fKnotenId == null) throw new Exception("Feld 'knotenId' in NetzwerkKnoten fehlt.");
        if ((string)fKnotenId.GetValue(knoten) != "Node-01")
            throw new Exception(
                "Konstruktor NetzwerkKnoten(string id) setzt 'knotenId' nicht. Füge hinzu: knotenId = id;");

        MethodInfo mValidiere = tKnoten.GetMethod("ValidierePaket") ?? tKnoten.GetMethod("validierePaket");
        if (mValidiere == null) throw new Exception("Methode 'ValidierePaket' in NetzwerkKnoten fehlt.");

        // test case 1: too short
        object p1 = ctorPaket.Invoke(new object[] { new[] { 10, 20 } });
        int res1 = (int)mValidiere.Invoke(knoten, new[] { p1 });
        if (res1 != -1)
            throw new Exception($"Längenprüfung fehlgeschlagen. Array < 3 sollte -1 zurückgeben, erhalten: {res1}");

        // test case 2: invalid checksum
        // payload sum: 10 + 20 + 30 = 60. checksum provided: 200
        object p2 = ctorPaket.Invoke(new object[] { new[] { 99, 10, 20, 30, 200 } });
        int res2 = (int)mValidiere.Invoke(knoten, new[] { p2 });
        if (res2 != -2)
            throw new Exception(
                $"Prüfsummen-Validierung fehlgeschlagen. Falsche Prüfsumme sollte -2 zurückgeben, erhalten: {res2}");

        // test case 3: valid packet
        // payload sum: 100 + 150 = 250. checksum provided: 250
        object p3 = ctorPaket.Invoke(new object[] { new[] { 42, 100, 150, 250 } });
        int res3 = (int)mValidiere.Invoke(knoten, new[] { p3 });

        if (res3 == 42)
        {
            feedback = "Nassi-Shneiderman Algorithmus exakt umgesetzt! Pakete werden korrekt validiert.";
            return true;
        }

        throw new Exception(
            $"Gültiges Paket wurde abgelehnt oder falscher Header zurückgegeben. Erwartet (Header): 42, Erhalten: {res3}");
    }

    private static object InvokeWithTimeout(MethodInfo method, object instance, object[] args, int timeoutMs = 500)
    {
        var task = Task.Run(() =>
        {
            try
            {
                return method.Invoke(instance, args);
            }
            catch (TargetInvocationException ex)
            {
                // Re-throw the actual exception from the user code
                throw ex.InnerException ?? ex;
            }
        });

        if (task.Wait(timeoutMs))
        {
            // Task completed within time
            if (task.IsFaulted) throw task.Exception.InnerException ?? task.Exception;
            return task.Result;
        }

        // Timeout occurred
        throw new Exception(
            "Zeitüberschreitung: Die Methode läuft zu lange (Eventuell eine Endlosschleife in 'while'?).");
    }

    private static bool TestLevel17(Assembly assembly, string sourceCode, out string feedback)
    {
        Type tReader = assembly.GetType("RFIDReader");
        Type tSerial = assembly.GetType("Serial");

        if (tReader == null) throw new Exception("Klasse 'RFIDReader' fehlt.");
        if (tSerial == null) throw new Exception("Hilfsklasse 'Serial' fehlt im Assembly.");

        ConstructorInfo ctorReader = tReader.GetConstructor(new[] { tSerial });
        if (ctorReader == null) throw new Exception("Konstruktor RFIDReader(Serial s) fehlt.");

        object serialMock = Activator.CreateInstance(tSerial, "COM1", 9600, 8, 1, 0);
        object reader = ctorReader.Invoke(new[] { serialMock });

        FieldInfo fReaderSerial = tReader.GetField("serial", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fReaderSerial == null) throw new Exception("Feld 'serial' fehlt in RFIDReader.");
        if (fReaderSerial.GetValue(reader) == null)
            throw new Exception("Das Serial-Objekt wurde im Konstruktor nicht gespeichert.");

        PropertyInfo pIsOpen = tSerial.GetProperty("IsOpen");
        bool isOpen = (bool)pIsOpen.GetValue(serialMock);
        if (!isOpen)
            throw new Exception(
                "Die serielle Schnittstelle wurde nicht geöffnet. Gemäß Abitur-Vorgabe müssen Sie im Konstruktor 'serial.Open()' aufrufen.");

        // test IsCardAvailable
        MethodInfo mAvail = tReader.GetMethod("IsCardAvailable") ?? tReader.GetMethod("isCardAvailable");
        if (mAvail == null) throw new Exception("Methode IsCardAvailable() fehlt.");

        bool isAvailEmpty = (bool)mAvail.Invoke(reader, null);
        if (isAvailEmpty)
            throw new Exception(
                "IsCardAvailable() meldet sofort true, obwohl keine Daten anliegen. Nutzen Sie die korrekte Bedingung (serial.DataAvailable() > 0).");

        MethodInfo mSetBytes = tSerial.GetMethod("SetTestBytes");
        mSetBytes.Invoke(serialMock, new object[] { new[] { 0x02 } });

        bool isAvail = (bool)mAvail.Invoke(reader, null);
        if (!isAvail)
            throw new Exception(
                "IsCardAvailable() gibt false zurück, obwohl Daten im Serial-Buffer liegen. Nutzen Sie serial.DataAvailable() > 0.");

        MethodInfo mReadSerial = tSerial.GetMethod("Read") ?? tSerial.GetMethod("read");
        mReadSerial.Invoke(serialMock, null);

        // test ReadCard
        int[] validPacket = new[] { 2, 49, 50, 51, 52, 53, 54, 7, 3 }; // STX, "123456", XOR(7), ETX
        mSetBytes.Invoke(serialMock, new object[] { validPacket });

        MethodInfo mReadCard = tReader.GetMethod("ReadCard") ?? tReader.GetMethod("readCard");
        if (mReadCard == null) throw new Exception("Methode ReadCard() fehlt.");

        string cardResult;
        try
        {
            cardResult = (string)InvokeWithTimeout(mReadCard, reader, null, 1000);
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler in ReadCard: {ex.Message}");
        }

        if (cardResult != "123456")
            throw new Exception(
                $"ReadCard liefert falsches Ergebnis. Erwartet \"123456\", erhalten \"{cardResult}\".\nPrüfen Sie: \n1. Lesen Sie genau 6 Datenbytes?\n2. Ist die XOR-Prüfung korrekt?\n3. Prüfen Sie auf ETX (0x03) am Ende?");

        feedback = "Klasse! Das Auslesen und Validieren des hardwarenahen Protokolls funktioniert fehlerfrei.";
        return true;
    }

    private static bool TestLevel18(Assembly assembly, string sourceCode, out string feedback)
    {
        Type tRover = assembly.GetType("Rover");
        Type tController = assembly.GetType("Controller");
        Type tSerial = assembly.GetType("Serial");
        Type tFunk = assembly.GetType("FunkModul");
        Type tReader = assembly.GetType("RFIDReader");

        if (tRover == null) throw new Exception("Klasse 'Rover' fehlt.");
        if (tController == null) throw new Exception("Klasse 'Controller' fehlt.");
        if (tReader == null) throw new Exception("Klasse 'RFIDReader' fehlt.");

        string strippedSource = Regex.Replace(sourceCode, @"//[^\r\n]*", "");
        strippedSource = Regex.Replace(strippedSource, @"/\*.*?\*/", "", RegexOptions.Singleline);
        string cleanSourceExact = strippedSource.Replace(" ", "").Replace("\r", "").Replace("\n", "");
        string cleanSourceLower = cleanSourceExact.ToLower();

        if (!cleanSourceLower.Contains("serial.open();"))
            throw new Exception(
                "Die serielle Schnittstelle muss im Konstruktor des Controllers explizit mit 'serial.Open();' geöffnet werden (Abitur-Standard).");

        if (!cleanSourceExact.Contains("this.id=id;"))
            throw new Exception("Konstruktor Rover: Sie müssen die ID exakt mit 'this.id = id;' zuweisen.");
        if (!cleanSourceExact.Contains("fahrzeugNr=++autowert;") &&
            !cleanSourceLower.Contains("fahrzeugnr=++autowert;"))
            throw new Exception(
                "Konstruktor Rover: Sie müssen die Fahrzeugnummer exakt mit 'fahrzeugNr = ++autowert;' setzen (Pre-Inkrement).");

        if (!cleanSourceLower.Contains("rover=r;") && !cleanSourceLower.Contains("this.rover=r;"))
            throw new Exception("Konstruktor Controller: Der Rover wurde nicht zugewiesen. Erwartet: rover = r;");

        if (!cleanSourceExact.Contains("serial=new(port,9600,8,1,0);") &&
            !cleanSourceExact.Contains("serial=newSerial(port,9600,8,1,0);") &&
            !cleanSourceExact.Contains("this.serial=new(port,9600,8,1,0);") &&
            !cleanSourceExact.Contains("this.serial=newSerial(port,9600,8,1,0);"))
            throw new Exception(
                "Konstruktor Controller: Der Serial-Port wurde nicht exakt initialisiert. Erwartet: serial = new(port, 9600, 8, 1, 0);");

        if (!cleanSourceExact.Contains("reader=new(serial);") &&
            !cleanSourceExact.Contains("reader=newRFIDReader(serial);") &&
            !cleanSourceExact.Contains("this.reader=new(serial);") &&
            !cleanSourceExact.Contains("this.reader=newRFIDReader(serial);"))
            throw new Exception(
                "Konstruktor Controller: Der Reader wurde nicht exakt initialisiert. Erwartet: reader = new(serial);");

        if (!cleanSourceExact.Contains("funk=new();") &&
            !cleanSourceExact.Contains("funk=newFunkModul();") &&
            !cleanSourceExact.Contains("this.funk=new();") &&
            !cleanSourceExact.Contains("this.funk=newFunkModul();"))
            throw new Exception(
                "Konstruktor Controller: Das Funkmodul wurde nicht exakt initialisiert. Erwartet: funk = new();");

        // --- TASK 1 ---
        ConstructorInfo ctorRover = tRover.GetConstructor(new[] { typeof(string) });
        if (ctorRover == null) throw new Exception("Konstruktor Rover(string id) fehlt.");

        object r1 = ctorRover.Invoke(new object[] { "R1" });
        object r2 = ctorRover.Invoke(new object[] { "R2" });

        MethodInfo mGetNr = tRover.GetMethod("GetFahrzeugNr") ?? tRover.GetMethod("getFahrzeugNr");
        int nr1 = (int)mGetNr.Invoke(r1, null);
        int nr2 = (int)mGetNr.Invoke(r2, null);

        if (nr1 < 1 || nr2 != nr1 + 1)
            throw new Exception(
                $"Autowert nicht korrekt. Rover 1 hat Nr {nr1}, Rover 2 hat Nr {nr2}. Erwartet: Fortlaufende Nummerierung.");

        // --- TASK 2 ---
        ConstructorInfo ctorController = tController.GetConstructor(new[] { typeof(string), tRover });
        if (ctorController == null) throw new Exception("Konstruktor Controller(string port, Rover r) fehlt.");

        object controller = null;
        try
        {
            controller = ctorController.Invoke(new[] { "COM1", r1 });
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler im Konstruktor: {ex.InnerException?.Message ?? ex.Message}");
        }

        FieldInfo fSerial = tController.GetField("serial", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fFunk = tController.GetField("funk", BindingFlags.NonPublic | BindingFlags.Instance) ??
                          tController.GetField("funkModul", BindingFlags.NonPublic | BindingFlags.Instance);

        MethodInfo mRun = tController.GetMethod("Run") ?? tController.GetMethod("run");
        if (mRun == null) throw new Exception("Methode Run() fehlt.");

        object ctrlSerial = fSerial.GetValue(controller);
        MethodInfo mSetBytesCtrl = tSerial.GetMethod("SetTestBytes");
        int[] validPacketCtrl = new[] { 2, 49, 50, 51, 52, 53, 54, 7, 3 }; // id: "123456"
        mSetBytesCtrl.Invoke(ctrlSerial, new object[] { validPacketCtrl });

        try
        {
            mRun.Invoke(controller, null);
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler während Run(): {ex.InnerException?.Message ?? ex.Message}");
        }

        // validate FunkModul input
        object funkObj = fFunk.GetValue(controller);
        MethodInfo mGetLastCmd = tFunk.GetMethod("GetLastCommand");
        string lastCmd = (string)mGetLastCmd.Invoke(funkObj, null);

        if (string.IsNullOrEmpty(lastCmd))
            throw new Exception("Es wurde kein Befehl über das FunkModul gesendet.");

        string expectedCmd = $"UNLOCK {nr1} 123456";
        if (lastCmd != expectedCmd)
            throw new Exception(
                $"Falscher Funk-Befehl gesendet. Erwartet: \"{expectedCmd}\", Erhalten: \"{lastCmd}\". Prüfen Sie Variablen und Leerzeichen.");

        if (!cleanSourceLower.Contains("rover.unlock();"))
            throw new Exception("Der Rover wurde nicht entriegelt (rover.Unlock() fehlt oder ist auskommentiert).");

        feedback =
            "Hervorragend! Sie haben die Hardware-Komponenten exakt initialisiert und erfolgreich durch den Controller verknüpft.";
        return true;
    }

    private static bool TestLevel19(Assembly assembly, out string feedback)
    {
        Type tZentrale = assembly.GetType("MissionsZentrale");
        Type tRover = assembly.GetType("Rover");
        Type tWartung = assembly.GetType("WartungsDienst");
        Type tTicket = assembly.GetType("WartungsTicket");
        Type tLog = assembly.GetType("LogEintrag");

        if (tZentrale == null) throw new Exception("Klasse 'MissionsZentrale' fehlt.");
        if (tRover == null) throw new Exception("Klasse 'Rover' fehlt.");
        if (tWartung == null) throw new Exception("Klasse 'WartungsDienst' fehlt.");
        if (tTicket == null) throw new Exception("Klasse 'WartungsTicket' fehlt.");
        if (tLog == null) throw new Exception("Klasse 'LogEintrag' fehlt.");

        // check whether LogEintrag constructor actually sets the fields
        ConstructorInfo ctorLog = tLog.GetConstructor(new[] { typeof(string), typeof(string) });
        if (ctorLog == null) throw new Exception("Konstruktor LogEintrag(string typ, string inhalt) fehlt.");
        object logTest = ctorLog.Invoke(new object[] { "ERR", "Testinhalt" });
        FieldInfo fLogTyp = tLog.GetField("typ", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fLogInhalt = tLog.GetField("inhalt", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fLogTyp == null) throw new Exception("Feld 'typ' in LogEintrag fehlt.");
        if (fLogInhalt == null) throw new Exception("Feld 'inhalt' in LogEintrag fehlt.");
        if ((string)fLogTyp.GetValue(logTest) != "ERR")
            throw new Exception(
                "Konstruktor LogEintrag(string typ, string inhalt) setzt 'typ' nicht. Füge hinzu: this.typ = typ;");
        if ((string)fLogInhalt.GetValue(logTest) != "Testinhalt")
            throw new Exception(
                "Konstruktor LogEintrag(string typ, string inhalt) setzt 'inhalt' nicht. Füge hinzu: this.inhalt = inhalt;");

        // check whether WartungsTicket constructor actually sets the fields
        ConstructorInfo ctorTicketCheck = tTicket.GetConstructor(new[] { typeof(string), typeof(string) });
        if (ctorTicketCheck == null) throw new Exception("Konstruktor WartungsTicket(string id, string grund) fehlt.");
        object ticketTest = ctorTicketCheck.Invoke(new object[] { "TESTID", "Testgrund" });
        FieldInfo fTicketRoverId = tTicket.GetField("roverId", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fTicketGrund = tTicket.GetField("grund", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fTicketRoverId == null) throw new Exception("Feld 'roverId' in WartungsTicket fehlt.");
        if (fTicketGrund == null) throw new Exception("Feld 'grund' in WartungsTicket fehlt.");
        if ((string)fTicketRoverId.GetValue(ticketTest) != "TESTID")
            throw new Exception(
                "Konstruktor WartungsTicket(string id, string grund) setzt 'roverId' nicht. Füge hinzu: roverId = id;");
        if ((string)fTicketGrund.GetValue(ticketTest) != "Testgrund")
            throw new Exception(
                "Konstruktor WartungsTicket(string id, string grund) setzt 'grund' nicht. Füge hinzu: this.grund = grund;");

        // 1. check WartungsDienst impl
        object wartung = Activator.CreateInstance(tWartung);
        MethodInfo mTicket = tWartung.GetMethod("ErstelleTicket") ?? tWartung.GetMethod("erstelleTicket");
        if (mTicket == null) throw new Exception("Methode 'ErstelleTicket' in 'WartungsDienst' fehlt.");

        // ensure WartungsDienst has list of tickets
        FieldInfo fTickets = tWartung.GetField("tickets", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fTickets == null)
            throw new Exception("Feld 'tickets' (Liste von WartungsTicket) in 'WartungsDienst' fehlt.");

        // initialize list if necessary (constructor check usually handles this but we want to fail gracefully if user forgot)
        if (fTickets.GetValue(wartung) == null)
            throw new Exception("Liste 'tickets' in WartungsDienst ist null. Initialisieren Sie diese im Konstruktor.");

        // 2. check rover implementation and battery logic
        ConstructorInfo ctorRover = tRover.GetConstructor(new[] { typeof(string) });
        if (ctorRover == null) throw new Exception("Konstruktor Rover(string id) fehlt.");
        object r1 = ctorRover.Invoke(new object[] { "CURIOSITY" });

        // check whether Rover.GetId() returns the actual id
        MethodInfo mGetId = tRover.GetMethod("GetId") ?? tRover.GetMethod("getId");
        if (mGetId == null) throw new Exception("Methode 'GetId' in Rover fehlt.");
        FieldInfo fRoverId = tRover.GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fRoverId == null) throw new Exception("Feld 'id' in Rover fehlt.");
        if ((string)fRoverId.GetValue(r1) != "CURIOSITY")
            throw new Exception("Konstruktor Rover(string id) setzt 'id' nicht korrekt. Füge hinzu: this.id = id;");
        if ((string)mGetId.Invoke(r1, null) != "CURIOSITY")
            throw new Exception("GetId() gibt nicht den im Konstruktor gesetzten Wert zurück. Prüfen Sie: return id;");
        FieldInfo fBatterie = tRover.GetField("batterie", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fBatterie == null) throw new Exception("Feld 'batterie' in Rover fehlt.");
        int battVal = (int)fBatterie.GetValue(r1);
        if (battVal != 100)
            throw new Exception($"Batterie startet bei {battVal}, sollte aber laut Diagramm bei 100 starten.");

        MethodInfo mStatus = tRover.GetMethod("VerarbeiteStatus") ?? tRover.GetMethod("verarbeiteStatus");
        if (mStatus == null) throw new Exception("Methode 'VerarbeiteStatus' in 'Rover' fehlt.");
        if (mStatus.ReturnType != typeof(bool))
            throw new Exception("Methode 'VerarbeiteStatus' muss 'bool' zurückgeben.");

        // test rover logic
        // case A: simple error -> battery -5 -> 95. not critical (false)
        bool res1 = (bool)mStatus.Invoke(r1, new object[] { "ERR", "SensorFehler" });
        if (res1)
            throw new Exception(
                "Rover meldet 'kritisch' (true), obwohl Batterie noch fast voll ist. (ERR soll -5 kosten).");

        // case b: set battery -> 15. critical (true)
        bool res2 = (bool)mStatus.Invoke(r1, new object[] { "BAT", "15" });
        if (!res2)
            throw new Exception("Rover meldet 'nicht kritisch' (false), obwohl Batterie auf 15 gesetzt wurde (< 20).");

        // 3. check interaction in MissionsZentrale
        object centrale = Activator.CreateInstance(tZentrale);

        MethodInfo mAddRover = tZentrale.GetMethod("AddRover") ?? tZentrale.GetMethod("addRover");
        MethodInfo mProcess =
            tZentrale.GetMethod("VerarbeiteDatenstrom") ?? tZentrale.GetMethod("verarbeiteDatenstrom");

        // re-create clean objects for integration test
        object rTest = ctorRover.Invoke(new object[] { "TESTROVER" });
        mAddRover.Invoke(centrale, new[] { rTest });

        // check if WartungsDienst is in MissionsZentrale
        FieldInfo fWartungCentral = tZentrale.GetField("wartung", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fWartungCentral == null) throw new Exception("Feld 'wartung' in MissionsZentrale fehlt.");
        object wInstance = fWartungCentral.GetValue(centrale);
        if (wInstance == null)
            throw new Exception("WartungsDienst wurde im Konstruktor der MissionsZentrale nicht initialisiert.");

        // scenario:
        // 1. "TESTROVER#BAT#25" -> battery 25 (not critical)
        // 2. "TESTROVER#ERR#Crash" -> battery 20 (not critical)
        // 3. "TESTROVER#ERR#Leak" -> battery 15 (critical) -> must trigger ticket
        string stream = "TESTROVER#BAT#25|TESTROVER#ERR#Crash|TESTROVER#ERR#Leak";

        try
        {
            mProcess.Invoke(centrale, new object[] { stream });
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler in 'VerarbeiteDatenstrom': {ex.InnerException?.Message ?? ex.Message}");
        }

        // check if ticket was created in the WartungsDienst instance
        IList ticketList = fTickets.GetValue(wInstance) as IList;

        if (ticketList == null || ticketList.Count == 0)
            throw new Exception(
                "Es wurde kein Ticket erstellt, obwohl der Rover einen kritischen Status (Batterie < 20) erreicht hat.");
        if (ticketList.Count > 1)
            throw new Exception(
                $"Es wurden zu viele Tickets erstellt ({ticketList.Count}). Es sollte erst beim Unterschreiten der Grenze von 20 ein Ticket erstellt werden.");

        // verify ticket type
        object ticket = ticketList[0];
        if (ticket.GetType() != tTicket)
            throw new Exception("Das Objekt in der Ticket-Liste ist nicht vom Typ 'WartungsTicket'.");

        // check if Ticket is actually set correctly
        string actualRoverId = (string)fTicketRoverId.GetValue(ticket);
        string actualGrund = (string)fTicketGrund.GetValue(ticket);
        if (actualRoverId != "TESTROVER")
            throw new Exception(
                $"Das Ticket enthält die falsche Rover-ID: \"{actualRoverId}\". Erwartet: \"TESTROVER\". Übergeben Sie r.GetId() an ErstelleTicket().");
        if (actualGrund != "Kritischer Batteriestatus")
            throw new Exception(
                $"Das Ticket enthält den falschen Grund: \"{actualGrund}\". Erwartet: \"Kritischer Batteriestatus\".");

        feedback =
            "Sektion 4 erfolgreich gemeistert! Datenstrom, Kollaboration und Protokolle funktionieren einwandfrei.";
        return true;
    }

    private static bool TestLevel20(Assembly assembly, out string feedback)
    {
        Type tServer = assembly.GetType("SmartHomeServer");
        Type tServerSocket = assembly.GetType("ServerSocket");
        Type tSocket = assembly.GetType("Socket");

        if (tServer == null) throw new Exception("Klasse 'SmartHomeServer' fehlt.");
        if (tServerSocket == null) throw new Exception("Hilfsklasse 'ServerSocket' fehlt.");
        if (tSocket == null) throw new Exception("Hilfsklasse 'Socket' fehlt.");

        int testPort = 54321;
        ConstructorInfo ctorServer = tServer.GetConstructor(new[] { typeof(int) });
        if (ctorServer == null) throw new Exception("Konstruktor SmartHomeServer(int port) fehlt.");

        object serverObj = null;
        try
        {
            serverObj = ctorServer.Invoke(new object[] { testPort });
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Fehler im Konstruktor von SmartHomeServer: {ex.InnerException?.Message ?? ex.Message}");
        }

        // validate attributes
        FieldInfo fPort = tServer.GetField("port", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fPort == null) throw new Exception("Das private Feld 'port' (int) fehlt in der Klasse SmartHomeServer.");

        int portVal = (int)fPort.GetValue(serverObj);
        if (portVal != testPort)
            throw new Exception(
                $"Das Attribut 'port' wurde im Konstruktor nicht korrekt zugewiesen. Erwartet: {testPort}, Gefunden: {portVal}");

        // validate ServerSocket init
        FieldInfo fServer = tServer.GetField("server", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fServer == null) throw new Exception("Das private Feld 'server' (ServerSocket) fehlt.");

        object serverSocketObj = fServer.GetValue(serverObj);
        if (serverSocketObj == null)
            throw new Exception("Das Feld 'server' wurde im Konstruktor nicht initialisiert.");

        FieldInfo fSsPort = tServerSocket.GetField("port", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fSsPort != null)
        {
            int ssPortVal = (int)fSsPort.GetValue(serverSocketObj);
            if (ssPortVal != testPort)
                throw new Exception(
                    $"Der 'ServerSocket' wurde mit dem falschen Port initialisiert. Erwartet: {testPort}, Gefunden: {ssPortVal}. Stellen Sie sicher, dass Sie den Parameter 'port' an den Konstruktor von ServerSocket übergeben.");
        }

        MethodInfo mStart = tServer.GetMethod("StartServer") ?? tServer.GetMethod("startServer");
        if (mStart == null) throw new Exception("Methode 'StartServer()' fehlt.");

        // mock data setup
        PropertyInfo pMockSocket = tServerSocket.GetProperty("MockSocket");
        object mockClientSocket = pMockSocket.GetValue(serverSocketObj);
        MethodInfo mSetInputs = tSocket.GetMethod("SetTestInputs");

        // simulated client commands testing loop and alt branches
        string testDeviceId = "Thermostat_Bad";
        mSetInputs.Invoke(mockClientSocket, new object[] { new[] { $"HELLO:{testDeviceId}", "STATUS_CHECK", "QUIT" } });

        // run StartServer (with timeout as precaution against infinite loop)
        try
        {
            InvokeWithTimeout(mStart, serverObj, null, 1500);
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler bei der Ausführung von StartServer(): {ex.Message}");
        }

        // output validation
        FieldInfo fOutputs = tSocket.GetField("Outputs");
        List<string> outputs = (List<string>)fOutputs.GetValue(mockClientSocket);

        if (outputs.Count == 0)
            throw new Exception("Der Server hat keine Antworten (write) an den Client gesendet.");

        if (outputs.Count < 3)
            throw new Exception(
                "Der Server hat zu wenige Antworten gesendet. Haben Sie eine Schleife implementiert, die solange liest, bis 'QUIT' empfangen wird?");

        string greeting = outputs[0];
        string ack = outputs[1];
        string err = outputs[2];

        if (!greeting.Contains("+OK Smart Home Hub") || !greeting.EndsWith("\n"))
            throw new Exception("Die Begrüßungsnachricht ist inkorrekt oder der Zeilenumbruch fehlt.");

        string expectedAck = $"+ACK {testDeviceId}\n";
        if (ack != expectedAck)
        {
            if (ack.Contains("HELLO:"))
                throw new Exception(
                    "Fehler bei der Substring-Extraktion. Die ID enthält noch das 'HELLO:'. Nutzen Sie Substring(6)!");
            throw new Exception(
                $"Das ACK-Protokoll ist inkorrekt. Erhalten: \"{ack.Trim()}\", Erwartet: \"{expectedAck.Trim() + "\\n"}\"");
        }

        if (!err.Contains("-ERR unbekannt") || !err.EndsWith("\n"))
            throw new Exception("Unbekannte Befehle (außer QUIT) müssen mit '-ERR unbekannt\\n' beantwortet werden.");

        feedback =
            "Hervorragend! Sie haben die Port-Initialisierung korrekt umgesetzt und die Befehlsverarbeitung erfolgreich in einer Schleife mit Fallunterscheidung implementiert.";
        return true;
    }

    private static bool TestLevel21(Assembly assembly, out string feedback)
    {
        Type tServer = assembly.GetType("SmartHomeServer");
        Type tLicht = assembly.GetType("Licht");
        Type tServerSocket = assembly.GetType("ServerSocket");
        Type tSocket = assembly.GetType("Socket");

        if (tServer == null) throw new Exception("Klasse 'SmartHomeServer' fehlt.");
        if (tLicht == null) throw new Exception("Klasse 'Licht' fehlt.");

        // check Licht
        ConstructorInfo ctorLicht = tLicht.GetConstructor(new[] { typeof(char) });
        if (ctorLicht == null) throw new Exception("Konstruktor Licht(char bez) fehlt.");

        object lichtObj = ctorLicht.Invoke(new object[] { 'A' });
        FieldInfo fIstAn = tLicht.GetField("istAn", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fIstAn == null) throw new Exception("Feld 'istAn' in der Klasse Licht fehlt.");

        MethodInfo mToggle = tLicht.GetMethod("Toggle") ?? tLicht.GetMethod("toggle");
        if (mToggle == null) throw new Exception("Methode 'Toggle()' in Licht fehlt.");

        bool initialState = (bool)fIstAn.GetValue(lichtObj);
        mToggle.Invoke(lichtObj, null);
        bool toggledState = (bool)fIstAn.GetValue(lichtObj);

        if (initialState == toggledState)
            throw new Exception("Die Methode 'Toggle()' ändert den Zustand von 'istAn' nicht.");

        // check SmartHomeServer
        int testPort = 8080;
        ConstructorInfo ctorServer = tServer.GetConstructor(new[] { typeof(int) });
        if (ctorServer == null) throw new Exception("Konstruktor SmartHomeServer(int port) fehlt.");

        object serverObj = null;
        try
        {
            serverObj = ctorServer.Invoke(new object[] { testPort });
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Fehler im Konstruktor von SmartHomeServer: {ex.InnerException?.Message ?? ex.Message}");
        }

        // check port assignment
        FieldInfo fPort = tServer.GetField("port", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fPort == null) throw new Exception("Feld 'port' in SmartHomeServer fehlt.");
        if ((int)fPort.GetValue(serverObj) != testPort)
            throw new Exception($"Das Attribut 'port' wurde im Konstruktor nicht zugewiesen. Erwartet: {testPort}.");

        // check if lichter were added in the constructor and are exactly A, B, C
        FieldInfo fLichter = tServer.GetField("lichter", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fLichter == null) throw new Exception("Feld 'lichter' (List<Licht>) fehlt in SmartHomeServer.");

        IList lichterList = fLichter.GetValue(serverObj) as IList;
        if (lichterList == null || lichterList.Count != 3)
            throw new Exception(
                "Die Liste 'lichter' muss im Konstruktor initialisiert und mit exakt drei Lichtern ('A', 'B', 'C') befüllt werden.");

        MethodInfo mGetBez = tLicht.GetMethod("GetBezeichnung") ?? tLicht.GetMethod("getBezeichnung");
        if (mGetBez == null) throw new Exception("Methode GetBezeichnung in Licht fehlt.");

        List<char> bezList = new List<char>();
        foreach (object licht in lichterList) bezList.Add((char)mGetBez.Invoke(licht, null));

        if (!bezList.Contains('A') || !bezList.Contains('B') || !bezList.Contains('C'))
            throw new Exception(
                "Die Liste 'lichter' muss exakt die Lichter mit den Bezeichnungen 'A', 'B' und 'C' enthalten.");

        // check server socket initialization with correct port
        FieldInfo fServer = tServer.GetField("server", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fServer == null) throw new Exception("Feld 'server' (ServerSocket) fehlt in SmartHomeServer.");
        object serverSocketObj = fServer.GetValue(serverObj);
        if (serverSocketObj == null) throw new Exception("ServerSocket wurde im Konstruktor nicht initialisiert.");

        FieldInfo fSsPort = tServerSocket.GetField("port", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fSsPort != null && (int)fSsPort.GetValue(serverSocketObj) != testPort)
            throw new Exception(
                "Der 'ServerSocket' wurde mit dem falschen Port initialisiert. Stellen Sie sicher, dass 'server = new ServerSocket(port);' genutzt wird.");

        MethodInfo mStart = tServer.GetMethod("StartServer") ?? tServer.GetMethod("startServer");
        if (mStart == null) throw new Exception("Methode 'StartServer()' fehlt.");

        // setup mocks and simulate socket sonnection
        PropertyInfo pMockSocket = tServerSocket.GetProperty("MockSocket");
        object mockClientSocket = pMockSocket.GetValue(serverSocketObj);
        MethodInfo mSetInputs = tSocket.GetMethod("SetTestInputs");

        string[] commands = new[]
        {
            "TOGGLE_LIGHT;B", // valid command
            "TOGGLE_LIGHT;a", // invalid id (lowercase 'a' -> fails ascii validation)
            "TOGGLE_LIGHT;#", // invalid id (symbol '#' -> fails ascii validation)
            "TOGGLE_LIGHT;X", // valid ascii but light doesnt exist ('X')
            "QUIT"
        };
        mSetInputs.Invoke(mockClientSocket, new object[] { commands });

        try
        {
            InvokeWithTimeout(mStart, serverObj, null, 1500);
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler bei der Ausführung von StartServer(): {ex.Message}");
        }

        FieldInfo fOutputs = tSocket.GetField("Outputs");
        List<string> outputs = (List<string>)fOutputs.GetValue(mockClientSocket);

        if (outputs.Count < 4)
            throw new Exception("Der Server hat nicht auf alle Befehle (außer QUIT) geantwortet.");

        if (!outputs[0].EndsWith("\n") || !outputs[1].EndsWith("\n") || !outputs[2].EndsWith("\n") ||
            !outputs[3].EndsWith("\n"))
            throw new Exception(
                "Die Formatierung einer oder mehrerer Server-Antworten ist inkorrekt. Prüfen Sie das geforderte Protokoll-Format exakt.");

        string okResp = outputs[0].Trim();
        string lowerCaseErrResp = outputs[1].Trim();
        string symbolErrResp = outputs[2].Trim();
        string notFoundResp = outputs[3].Trim();

        if (okResp != "+OK")
            throw new Exception($"Erwartete Antwort '+OK\\n' für gültiges Licht 'B', aber erhalten: '{okResp}'.");

        // check for misaligned else blocks (common when converting sequence diagrams)
        if (lowerCaseErrResp == "-ERR Licht nicht gefunden" || symbolErrResp == "-ERR Licht nicht gefunden")
            throw new Exception(
                "Es wird eine falsche Fehlermeldung bei ungültigen IDs ausgegeben. Prüfen Sie die Schachtelung und Zuordnung Ihrer if-else-Blöcke (welches 'else' gehört zu welchem 'if'?).");

        if (lowerCaseErrResp != "-ERR ungültige ID" || symbolErrResp != "-ERR ungültige ID")
            throw new Exception(
                "Die Validierung der ID funktioniert nicht korrekt. Stellen Sie sicher, dass nur Großbuchstaben (A-Z) akzeptiert werden.");

        if (notFoundResp != "-ERR Licht nicht gefunden")
            throw new Exception(
                $"Wenn ein gültiges Licht nicht in der Liste existiert, muss '-ERR Licht nicht gefunden' gesendet werden. Erhalten: '{notFoundResp}'.");

        feedback =
            "Perfekt! Sie haben die Gerätesteuerung, die ASCII-Validierung und das Sequenzdiagramm fehlerfrei implementiert.";
        return true;
    }

    private static bool TestLevel22(Assembly assembly, string sourceCode, out string feedback)
    {
        Type tServer = assembly.GetType("SmartHomeServer");
        Type tThread = assembly.GetType("ServerThread");
        Type tHub = assembly.GetType("SmartHomeHub");
        Type tSocket = assembly.GetType("Socket");
        Type tServerSocket = assembly.GetType("ServerSocket");
        Type tThreadBase = assembly.GetType("Thread");

        if (tServer == null) throw new Exception("Klasse 'SmartHomeServer' fehlt.");
        if (tThread == null) throw new Exception("Klasse 'ServerThread' fehlt.");
        if (tHub == null) throw new Exception("Klasse 'SmartHomeHub' fehlt.");

        // TEST 1: SmartHomeServer check (static analysis due to infinite loop)
        string cleanSource = Regex.Replace(sourceCode, @"//[^\r\n]*", "");
        cleanSource = Regex.Replace(cleanSource, @"/\*.*?\*/", "", RegexOptions.Singleline).Replace(" ", "")
            .Replace("\r", "").Replace("\n", "").ToLower();

        if (!cleanSource.Contains("while(true)") && !cleanSource.Contains("for(;;)"))
            throw new Exception(
                "Die Methode RunServer() in SmartHomeServer muss eine Endlosschleife enthalten (z. B. while(true)), um dauerhaft Clients zu akzeptieren.");

        if (!cleanSource.Contains("serverthread") || !cleanSource.Contains(".start()"))
            throw new Exception(
                "In RunServer() muss ein neuer ServerThread instanziiert und mit .Start() gestartet werden.");

        // check server constructor
        ConstructorInfo ctorServer = tServer.GetConstructor(new[] { typeof(int), tHub });
        if (ctorServer == null) throw new Exception("Konstruktor SmartHomeServer(int port, SmartHomeHub hub) fehlt.");

        int testPort = 8080;
        object testHub = Activator.CreateInstance(tHub);
        object serverObj = null;
        try
        {
            serverObj = ctorServer.Invoke(new[] { testPort, testHub });
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Fehler im Konstruktor von SmartHomeServer: {ex.InnerException?.Message ?? ex.Message}");
        }

        FieldInfo fPort = tServer.GetField("port", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fPort == null) throw new Exception("Das Feld 'port' (int) in SmartHomeServer fehlt.");
        if ((int)fPort.GetValue(serverObj) != testPort)
            throw new Exception(
                $"Das Attribut 'port' wurde im Konstruktor nicht korrekt zugewiesen. Erwartet: {testPort}.");

        FieldInfo fServerHub = tServer.GetField("hub", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fServerHub == null) throw new Exception("Das Feld 'hub' (SmartHomeHub) in SmartHomeServer fehlt.");
        if (fServerHub.GetValue(serverObj) != testHub)
            throw new Exception("Das Attribut 'hub' wurde im Konstruktor nicht korrekt zugewiesen.");

        FieldInfo fServerSocket = tServer.GetField("serverSocket", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                  tServer.GetField("server", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fServerSocket == null)
            throw new Exception("Das Feld 'serverSocket' (ServerSocket) in SmartHomeServer fehlt.");

        object serverSocketObj = fServerSocket.GetValue(serverObj);
        if (serverSocketObj == null)
            throw new Exception("Das Feld 'serverSocket' wurde im Konstruktor nicht initialisiert.");

        FieldInfo fSsPort = tServerSocket.GetField("port", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fSsPort != null && (int)fSsPort.GetValue(serverSocketObj) != testPort)
            throw new Exception(
                "Der 'ServerSocket' wurde mit dem falschen Port initialisiert. Stellen Sie sicher, dass 'serverSocket = new ServerSocket(port);' genutzt wird.");

        // TEST 2: ServerThread check (dynamic test)
        if (!tThread.IsSubclassOf(tThreadBase))
            throw new Exception(
                "Die Klasse 'ServerThread' muss von 'Thread' erben (public class ServerThread : Thread).");

        ConstructorInfo ctorThread = tThread.GetConstructor(new[] { tSocket, tHub });
        if (ctorThread == null) throw new Exception("Konstruktor ServerThread(Socket cs, SmartHomeHub hub) fehlt.");

        MethodInfo mRun =
            tThread.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) ??
            tThread.GetMethod("run", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        if (mRun == null)
            throw new Exception(
                "Sie müssen die Methode 'Run()' überschreiben (verwenden Sie 'public override void Run()').");

        if (mRun.GetBaseDefinition().DeclaringType == mRun.DeclaringType)
            throw new Exception("Sie müssen die Methode 'Run()' mit dem Schlüsselwort 'override' überschreiben.");

        // setup mocks
        object hubObj = Activator.CreateInstance(tHub);
        object mockSocket = Activator.CreateInstance(tSocket, "localhost", 80);
        object serverThreadObj = ctorThread.Invoke(new[] { mockSocket, hubObj });

        // validate fields in ServerThread
        FieldInfo fClientSocket = tThread.GetField("clientSocket", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fClientSocket == null || fClientSocket.GetValue(serverThreadObj) == null)
            throw new Exception(
                "Das private Feld 'clientSocket' (Socket) fehlt in ServerThread oder wurde im Konstruktor nicht zugewiesen.");

        FieldInfo fHub = tThread.GetField("hub", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fHub == null || fHub.GetValue(serverThreadObj) == null)
            throw new Exception(
                "Das private Feld 'hub' (SmartHomeHub) fehlt in ServerThread oder wurde im Konstruktor nicht zugewiesen.");

        // inject commands
        MethodInfo mSetInputs = tSocket.GetMethod("SetTestInputs");
        string[] commands = new[] { "PING", "INFO", "LOGIN", "UNKNOWN", "QUIT" }; // INFO hinzugefügt
        mSetInputs.Invoke(mockSocket, new object[] { commands });

        // execute Run() with timeout
        try
        {
            InvokeWithTimeout(mRun, serverThreadObj, null, 1500);
        }
        catch (Exception ex)
        {
            throw new Exception($"Fehler bei der Ausführung von Run() im ServerThread: {ex.Message}");
        }

        // verify outputs
        FieldInfo fOutputs = tSocket.GetField("Outputs");
        List<string> outputs = (List<string>)fOutputs.GetValue(mockSocket);

        if (outputs.Count < 3)
            throw new Exception("Der ServerThread hat nicht ausreichend auf die Befehle geantwortet.");

        if (!outputs[0].EndsWith("\n") || !outputs[1].EndsWith("\n") || !outputs[2].EndsWith("\n"))
            throw new Exception(
                "Die Formatierung einer oder mehrerer Server-Antworten ist inkorrekt. Prüfen Sie das geforderte Protokoll-Format exakt.");

        string pongResp = outputs[0].Trim();
        string infoResp = outputs[1].Trim();
        string tokenResp = outputs[2].Trim();

        if (pongResp != "+PONG")
            throw new Exception($"Erwartete Antwort '+PONG\\n' auf Befehl 'PING', aber erhalten: '{pongResp}'.");

        if (infoResp != "+IP 127.0.0.1")
            throw new Exception(
                $"Auf 'INFO' muss der Server mit der korrekten IP antworten. Erhalten: '{infoResp}'. Haben Sie clientSocket.GetRemoteHostIP() genutzt?");

        if (!tokenResp.StartsWith("+TOKEN "))
            throw new Exception(
                $"Auf 'LOGIN' muss der Server mit '+TOKEN [Zufallszahl]' antworten. Erhalten: '{tokenResp}'.");

        if (!cleanSource.Contains("newrandom()") && !cleanSource.Contains("nextint("))
            throw new Exception(
                "Es sieht so aus, als hätten Sie die Random-Klasse nicht verwendet, um den Token zu generieren. Erstellen Sie ein Objekt der Hilfsklasse Random.");

        string[] tokenParts = tokenResp.Split(' ');
        if (tokenParts.Length < 2 || !int.TryParse(tokenParts[1], out int tokenValue))
            throw new Exception("Der generierte Token enthält keine gültige Zahl.");

        if (tokenValue < 0 || tokenValue > 9999)
            throw new Exception(
                $"Die generierte Zufallszahl ({tokenValue}) liegt nicht im geforderten Bereich (0 bis 9999). Nutzen Sie NextInt(10000).");

        feedback =
            "Klasse! Sie haben das Multi-User-Konzept erfolgreich verinnerlicht, Threading-Klassen korrekt abgeleitet und den ServerThread implementiert.";
        return true;
    }

    private static bool TestLevel23(Assembly assembly, string sourceCode, out string feedback)
    {
        Type tServer = assembly.GetType("SicherheitsServer");
        Type tThread = assembly.GetType("SicherheitsThread");
        Type tZentrale = assembly.GetType("SicherheitsZentrale");
        Type tSocket = assembly.GetType("Socket");
        Type tThreadBase = assembly.GetType("Thread");

        if (tServer == null || tThread == null)
            throw new Exception("Es wurden nicht alle geforderten Klassen implementiert. Prüfen Sie das Diagramm.");

        // check SicherheitsServer constructor
        ConstructorInfo ctorServer = tServer.GetConstructor(new[] { typeof(int), tZentrale });
        if (ctorServer == null)
            throw new Exception("Der Konstruktor von SicherheitsServer entspricht nicht den Vorgaben im UML-Diagramm.");

        object testZentrale = Activator.CreateInstance(tZentrale);
        int testPort = 8080;
        object serverObj = null;
        try
        {
            serverObj = ctorServer.Invoke(new[] { testPort, testZentrale });
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Fehler im Konstruktor von SicherheitsServer: {ex.InnerException?.Message ?? ex.Message}");
        }

        FieldInfo fPort = tServer.GetField("port", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fZentrale = tServer.GetField("zentrale", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fServerSocket = tServer.GetField("serverSocket", BindingFlags.NonPublic | BindingFlags.Instance) ??
                                  tServer.GetField("server", BindingFlags.NonPublic | BindingFlags.Instance);

        bool isServerValid = true;
        if (fPort == null || (int)fPort.GetValue(serverObj) != testPort) isServerValid = false;
        if (fZentrale == null || fZentrale.GetValue(serverObj) != testZentrale) isServerValid = false;
        if (fServerSocket == null || fServerSocket.GetValue(serverObj) == null)
        {
            isServerValid = false;
        }
        else
        {
            Type tServerSocketTest = assembly.GetType("ServerSocket");
            FieldInfo fSsPort = tServerSocketTest.GetField("port", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fSsPort != null && (int)fSsPort.GetValue(fServerSocket.GetValue(serverObj)) != testPort)
                isServerValid = false;
        }

        if (!isServerValid)
            throw new Exception(
                "Der Konstruktor von SicherheitsServer initialisiert die Attribute oder Objekte nicht korrekt. Prüfen Sie das Klassendiagramm und Ihre Zuweisungen sorgfältig.");

        if (!tThread.IsSubclassOf(tThreadBase))
            throw new Exception("Die Klasse SicherheitsThread erbt nicht korrekt von Thread.");

        ConstructorInfo ctorThread = tThread.GetConstructor(new[] { tSocket, tZentrale });
        if (ctorThread == null)
            throw new Exception("Der Konstruktor von SicherheitsThread entspricht nicht den Vorgaben im UML-Diagramm.");

        MethodInfo mRun =
            tThread.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) ??
            tThread.GetMethod("run", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        if (mRun == null)
            throw new Exception(
                "Sie müssen die Methode 'Run()' überschreiben (verwenden Sie 'public override void Run()').");

        if (mRun.GetBaseDefinition().DeclaringType == mRun.DeclaringType)
            throw new Exception("Sie müssen die Methode 'Run()' mit dem Schlüsselwort 'override' überschreiben.");

        MethodInfo mVergleiche =
            tThread.GetMethod("VergleicheZugangsdaten",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance) ?? tThread.GetMethod(
                "vergleicheZugangsdaten", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (mVergleiche == null)
            throw new Exception(
                "Die Methode 'VergleicheZugangsdaten' fehlt in SicherheitsThread. Halten Sie sich exakt an das Klassendiagramm.");
        if (mVergleiche.ReturnType != typeof(bool))
            throw new Exception("Die Methode 'VergleicheZugangsdaten' muss einen boolean zurückgeben.");

        var parameters = mVergleiche.GetParameters();
        if (parameters.Length != 2 || parameters[0].ParameterType != typeof(string) ||
            parameters[1].ParameterType != typeof(string))
            throw new Exception(
                "Die Methode 'VergleicheZugangsdaten' muss exakt zwei Parameter vom Typ string (user, pin) erwarten.");

        // static code check for structural awareness
        string cleanSource = Regex.Replace(sourceCode, @"//[^\r\n]*", "");
        cleanSource = Regex.Replace(cleanSource, @"/\*.*?\*/", "", RegexOptions.Singleline).Replace(" ", "")
            .Replace("\r", "").Replace("\n", "").ToLower();

        if (!cleanSource.Contains("getadminuser") || !cleanSource.Contains("getadminpin"))
            throw new Exception(
                "Sie haben die Getter für 'adminUser' und 'adminPin' der BenutzerVerwaltung nicht genutzt.");

        if (!cleanSource.Contains("getaktiv") || !cleanSource.Contains("setaktiv"))
            throw new Exception(
                "Der Status der Alarmanlage muss zwingend über deren Getter und Setter (GetAktiv / SetAktiv) abgerufen und modifiziert werden.");

        if (!cleanSource.Contains("vergleichezugangsdaten("))
            throw new Exception(
                "Sie haben die Methode 'VergleicheZugangsdaten' zwar deklariert, aber rufen sie nicht auf (Self-Call aus dem Sequenzdiagramm fehlt).");

        // setup mocks and instance
        object zueObj = Activator.CreateInstance(tZentrale);
        object mockSocket = Activator.CreateInstance(tSocket, "localhost", 80);
        object threadObj = ctorThread.Invoke(new[] { mockSocket, zueObj });

        // scenario 1: valid login and commands execution
        MethodInfo mSetInputs = tSocket.GetMethod("SetTestInputs");
        string[] commands = new[] { "LOGIN;Admin;1234", "STATUS", "TOGGLE", "STATUS", "QUIT" };
        mSetInputs.Invoke(mockSocket, new object[] { commands });

        try
        {
            InvokeWithTimeout(mRun, threadObj, null, 1500);
        }
        catch (Exception ex)
        {
            throw new Exception($"Laufzeitfehler im SicherheitsThread bei gültigem Login: {ex.Message}");
        }

        FieldInfo fOutputs = tSocket.GetField("Outputs");
        List<string> outputs = (List<string>)fOutputs.GetValue(mockSocket);

        if (outputs.Count < 5)
            throw new Exception(
                "Das Protokoll wurde nicht vollständig umgesetzt. Der Server antwortet nicht auf alle übermittelten Befehle.");

        for (int i = 0; i < 5; i++)
            if (!outputs[i].EndsWith("\n"))
                throw new Exception(
                    "Die Formatierung einer oder mehrerer Server-Antworten ist inkorrekt. Prüfen Sie das geforderte Protokoll-Format exakt.");

        if (outputs[0].Trim() != "+OK Willkommen")
            throw new Exception(
                $"Falsche Antwort auf korrekten Login. Erwartet: '+OK Willkommen\\n', Erhalten: '{outputs[0].Trim()}'");

        if (outputs[1].Trim() != "+OK ALARM_ON")
            throw new Exception(
                $"Falsche Antwort auf STATUS. Erwartet: '+OK ALARM_ON\\n', Erhalten: '{outputs[1].Trim()}'");

        if (outputs[2].Trim() != "+OK Umschaltung erfolgreich")
            throw new Exception(
                $"Falsche Antwort auf TOGGLE. Erwartet: '+OK Umschaltung erfolgreich\\n', Erhalten: '{outputs[2].Trim()}'");

        if (outputs[3].Trim() != "+OK ALARM_OFF")
            throw new Exception(
                "Die Alarmanlage wurde durch TOGGLE nicht ausgeschaltet (STATUS meldet immer noch ON).");

        if (outputs[4].Trim() != "+OK Bye")
            throw new Exception("Falsche Antwort auf QUIT oder die Schleife wurde danach nicht korrekt beendet.");

        // internal state assertions
        MethodInfo mGetAlarm = tZentrale.GetMethod("GetAlarmanlage") ?? tZentrale.GetMethod("getAlarmanlage");
        object alarmObj = mGetAlarm.Invoke(zueObj, null);
        MethodInfo mGetAktiv = alarmObj.GetType().GetMethod("GetAktiv") ?? alarmObj.GetType().GetMethod("getAktiv");
        bool isAktiv = (bool)mGetAktiv.Invoke(alarmObj, null);
        if (isAktiv)
            throw new Exception(
                "Der TOGGLE Befehl hat den Status der Alarmanlage auf Objektebene nicht verändert. Nutzen Sie SetAktiv(!GetAktiv()).");

        MethodInfo mGetLog = tZentrale.GetMethod("GetLog") ?? tZentrale.GetMethod("getLog");
        object logObj = mGetLog.Invoke(zueObj, null);
        MethodInfo mGetEintraege =
            logObj.GetType().GetMethod("GetEintraege") ?? logObj.GetType().GetMethod("getEintraege");
        IList eintraege = mGetEintraege.Invoke(logObj, null) as IList;

        if (eintraege == null || eintraege.Count == 0 || !eintraege[0].ToString().Contains("umgeschaltet"))
            throw new Exception(
                "Bei TOGGLE wurde kein entsprechender Eintrag in die 'eintraege'-Liste des ProtokollLogs eingefügt.");

        // scenario 2: invalid login test
        object mockSocketFail = Activator.CreateInstance(tSocket, "localhost", 80);
        object threadObjFail = ctorThread.Invoke(new[] { mockSocketFail, zueObj });
        mSetInputs.Invoke(mockSocketFail, new object[] { new[] { "LOGIN;Admin;0000", "STATUS" } });

        try
        {
            InvokeWithTimeout(mRun, threadObjFail, null, 1000);
        }
        catch (Exception ex)
        {
            throw new Exception($"Laufzeitfehler bei fehlerhaftem Login: {ex.Message}");
        }

        List<string> outputsFail = (List<string>)fOutputs.GetValue(mockSocketFail);

        if (outputsFail.Count == 0)
            throw new Exception(
                "Fehlerhafter Login wurde nicht korrekt abgewiesen. Erwartet: '-ERR Login fehlgeschlagen\\n'.");

        if (!outputsFail[0].EndsWith("\n"))
            throw new Exception(
                "Die Formatierung der Server-Antwort bei fehlgeschlagenem Login ist inkorrekt. Prüfen Sie das geforderte Protokoll-Format exakt.");

        if (outputsFail[0].Trim() != "-ERR Login fehlgeschlagen")
            throw new Exception(
                "Fehlerhafter Login wurde nicht korrekt abgewiesen. Erwartet: '-ERR Login fehlgeschlagen\\n'.");

        if (outputsFail.Count > 1)
            throw new Exception(
                "Nach einem fehlerhaften Login darf der Server keine weiteren Befehle verarbeiten (Verbindung muss beendet werden).");

        feedback =
            "Herzlichen Glückwunsch! Sie haben das Mini-Exam bestanden und das komplette Client-Server-Sicherheitsmodell wie im Abitur gefordert implementiert.";
        return true;
    }

    private static bool TestLevel24(Assembly assembly, string sourceCode, out string feedback)
    {
        Type tFlug = assembly.GetType("Flug");
        Type tPassagier = assembly.GetType("Passagier");
        Type tGepaeckWagen = assembly.GetType("GepaeckWagen");

        if (tFlug == null || tPassagier == null || tGepaeckWagen == null)
            throw new Exception("Es wurden nicht alle geforderten Klassen implementiert.");

        // check flug constructor
        ConstructorInfo ctorFlug = tFlug.GetConstructors().FirstOrDefault(c => c.GetParameters().Length >= 6);
        if (ctorFlug == null)
            throw new Exception("Initialisierung in Klasse Flug fehlerhaft..");

        object flugObj = null;
        DateTime testDate = DateTime.Now;
        object testDateObj = testDate;

        // check if user implemented LocalDate instead of DateTime
        ParameterInfo[] flugParams = ctorFlug.GetParameters();
        if (flugParams.Length >= 6 && flugParams[2].ParameterType.Name == "LocalDate")
        {
            Type tLocalDate = assembly.GetType("LocalDate");
            if (tLocalDate != null) testDateObj = tLocalDate.GetMethod("Now").Invoke(null, null);
        }

        try
        {
            flugObj = ctorFlug.Invoke(new[] { "LH123", "BER", testDateObj, "A1", 100, 150.0 });
        }
        catch
        {
            throw new Exception("Initialisierung der Klasse Flug fehlerhaft.");
        }

        // strict flug attributes check
        FieldInfo fFlugNr = tFlug.GetField("flugNr", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fZiel = tFlug.GetField("ziel", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fStartDatum = tFlug.GetField("startDatum", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fGate = tFlug.GetField("gate", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fMax = tFlug.GetField("maxPassagiere", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fPreis = tFlug.GetField("basisPreis", BindingFlags.NonPublic | BindingFlags.Instance);

        if (fFlugNr == null || fZiel == null || fStartDatum == null || fGate == null || fMax == null || fPreis == null)
            throw new Exception("Initialisierung in Klasse Flug fehlerhaft.");

        object actualDate = fStartDatum.GetValue(flugObj);
        bool isDateValid = actualDate != null && (actualDate.Equals(testDate) || actualDate.Equals(testDateObj));

        if ((string)fFlugNr.GetValue(flugObj) != "LH123" || (string)fZiel.GetValue(flugObj) != "BER" ||
            !isDateValid || (string)fGate.GetValue(flugObj) != "A1" ||
            (int)fMax.GetValue(flugObj) != 100 || Math.Abs((double)fPreis.GetValue(flugObj) - 150.0) > 0.01)
            throw new Exception("Initialisierung in Klasse Flug fehlerhaft.");

        // check passagier constructor and multiplicity
        ConstructorInfo ctorPassagier = tPassagier.GetConstructor(new[] { typeof(string), typeof(string), tFlug });
        if (ctorPassagier == null)
            throw new Exception("Initialisierung in Klasse Passagier fehlerhaft.");

        // reset autowert to make sure it starts at 0 (for testing)
        FieldInfo fAutowert = tPassagier.GetField("autowert", BindingFlags.NonPublic | BindingFlags.Static);
        if (fAutowert != null) fAutowert.SetValue(null, 0);

        object pass1 = null;
        object pass2 = null;
        try
        {
            pass1 = ctorPassagier.Invoke(new[] { "Max Mustermann", "T123", flugObj });
            pass2 = ctorPassagier.Invoke(new[] { "Erika Musterfrau", "T124", flugObj });
        }
        catch
        {
            throw new Exception("Fehler mit Klasse Passagier.");
        }

        // strict passagier attributes check
        FieldInfo fName = tPassagier.GetField("name", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fTicket = tPassagier.GetField("ticketNummer", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fFlugAttr = tPassagier.GetField("flug", BindingFlags.NonPublic | BindingFlags.Instance);

        if (fName == null || fTicket == null || fFlugAttr == null)
            throw new Exception("Initialisierung in Klasse Passagier fehlerhaft.");

        if ((string)fName.GetValue(pass1) != "Max Mustermann" || (string)fTicket.GetValue(pass1) != "T123" ||
            fFlugAttr.GetValue(pass1) != flugObj)
            throw new Exception("Initialisierung in Klasse Passagier fehlerhaft.");

        // check passagier internal logic (autowert, list initialization)
        FieldInfo fId = tPassagier.GetField("passagierID", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo fGepaeck = tPassagier.GetField("gepaeck", BindingFlags.NonPublic | BindingFlags.Instance);

        int id1 = (int)fId.GetValue(pass1);
        int id2 = (int)fId.GetValue(pass2);

        if (id1 != 1 || id2 != 2)
            throw new Exception("ID-Vergabe fehlerhaft.");

        if (fGepaeck.GetValue(pass1) == null)
            throw new Exception("Initialisierung in Klasse Passagier fehlerhaft.");

        // check gepaeckwagen (linked list logic)
        ConstructorInfo ctorWagen = tGepaeckWagen.GetConstructors().FirstOrDefault();
        if (ctorWagen == null)
            throw new Exception("Invalider Konstruktor der Klasse GepaeckWagen.");

        MethodInfo mAnhaengen = tGepaeckWagen.GetMethod("Anhaengen") ?? tGepaeckWagen.GetMethod("anhaengen");
        if (mAnhaengen == null)
            throw new Exception("Fehler mit Methode 'anhaengen()'.");

        // create mock data for abstract dependency
        Type tKoffer = assembly.GetType("Koffer");
        object mockGepaeck1 = null;
        object mockGepaeck2 = null;
        object mockGepaeck3 = null;
        object mockGepaeck4 = null;

        if (tKoffer != null)
        {
            ConstructorInfo ctorKoffer = tKoffer.GetConstructors().FirstOrDefault();
            if (ctorKoffer != null)
            {
                mockGepaeck1 = ctorKoffer.Invoke(new object[] { "K1", 10.0, true });
                mockGepaeck2 = ctorKoffer.Invoke(new object[] { "K2", 15.0, false });
                mockGepaeck3 = ctorKoffer.Invoke(new object[] { "K3", 20.0, true });
                mockGepaeck4 = ctorKoffer.Invoke(new object[] { "K4", 25.0, false });
            }
        }

        object wagen1 = ctorWagen.Invoke(new[] { mockGepaeck1 });
        object wagen2 = ctorWagen.Invoke(new[] { mockGepaeck2 });
        object wagen3 = ctorWagen.Invoke(new[] { mockGepaeck3 });
        object wagen4 = ctorWagen.Invoke(new[] { mockGepaeck4 });

        try
        {
            // utilizing InvokeWithTimeout to prevent infinite loops from circular references
            InvokeWithTimeout(mAnhaengen, wagen1, new[] { wagen2 }, 1000);
            InvokeWithTimeout(mAnhaengen, wagen1, new[] { wagen3 }, 1000);
            InvokeWithTimeout(mAnhaengen, wagen1, new[] { wagen4 }, 1000);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Zeitüberschreitung") || ex is TimeoutException)
                throw new Exception("Laufzeitfehler: Mögliche Endlosschleife in anhaengen().");

            throw new Exception($"Fehler bei der Ausführung der Methode anhaengen(): {ex.Message}");
        }

        FieldInfo fNaechster = tGepaeckWagen.GetField("naechsterWagen", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fNaechster == null)
            throw new Exception("Die Assoziation 'naechsterWagen' in GepaeckWagen wurde nicht korrekt umgesetzt.");

        object next1 = fNaechster.GetValue(wagen1);
        object next2 = next1 != null ? fNaechster.GetValue(next1) : null;
        object next3 = next2 != null ? fNaechster.GetValue(next2) : null;

        if (next1 != wagen2 || next2 != wagen3 || next3 != wagen4)
            throw new Exception("Die Methode anhaengen() verknüpft die Elemente der Liste nicht wie erwartet am Ende.");

        feedback = "Klassen, Multiplizitäten und die einfach verkettete Liste wurden fehlerfrei umgesetzt.";
        return true;
    }

    private static bool TestLevel25(Assembly assembly, string sourceCode, out string feedback)
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        Type tScanner = assembly.GetType("BarcodeScanner");
        Type tController = assembly.GetType("ScannerController");
        Type tSerial = assembly.GetType("Serial");
        Type tVerwaltung = assembly.GetType("FlughafenVerwaltung");
        Type tSchleuse = assembly.GetType("GepaeckSchleuse");

        if (tScanner == null || tController == null)
            throw new Exception("Es wurden nicht alle geforderten Klassen implementiert.");

        if (tSerial == null || tVerwaltung == null || tSchleuse == null)
            throw new Exception("Notwendige Hilfsklassen oder Abhängigkeiten fehlen im System.");

        // 1. anti-exploit checks
        string cleanSource = Regex.Replace(sourceCode, @"//[^\r\n]*", "");
        cleanSource = Regex.Replace(cleanSource, @"/\*.*?\*/", "", RegexOptions.Singleline).Replace(" ", "")
            .Replace("\r", "").Replace("\n", "").ToLower();

        if (!cleanSource.Contains("serial.open();"))
            throw new Exception("Fehler mit serieller Schnitstelle.");

        if (!cleanSource.Contains("calcchecksum("))
            throw new Exception("Fehler in 'Run()' bezüglich der 'CalcChecksum()' Methode.");

        if (!cleanSource.Contains("scanner.isready()"))
            throw new Exception("Fehler in 'Run()' bezüglich der Bereitschaft.");

        if (!cleanSource.Contains("verwaltung.getlogs().add(newlog(\"abgewiesen\""))
            throw new Exception("Fehler mit Log-Einträgen in 'Run()'.");

        if (!cleanSource.Contains("newserial(comport,9600,8,1,0)") && !cleanSource.Contains("new(comport,9600,8,1,0)"))
            throw new Exception("Fehler mit serieller Schnitstelle.");

        if (!cleanSource.Contains("thread.sleep") && !cleanSource.Contains("task.delay"))
            throw new Exception("Fehler in 'Run()': Die Gepäckschleuse muss für eine gewisse Zeit geöffnet bleiben.");

        if (!cleanSource.Contains("0x02") && !cleanSource.Contains("==2") && !cleanSource.Contains("!=2"))
            throw new Exception("Fehler in 'ReadBarcode()'.");

        if (!cleanSource.Contains("0x03") && !cleanSource.Contains("==3") && !cleanSource.Contains("!=3"))
            throw new Exception("Fehler in 'ReadBarcode()'.");

        // 2. test: barcodescanner and calcchecksum
        ConstructorInfo ctorScanner = tScanner.GetConstructor(new[] { tSerial });
        if (ctorScanner == null)
            throw new Exception("Invalider Konstruktor des BarcodeScanners.");

        object serialMock = Activator.CreateInstance(tSerial, "COM1", 9600, 8, 1, 0);
        object scannerObj = ctorScanner.Invoke(new[] { serialMock });

        MethodInfo mCalc = tScanner.GetMethod("CalcChecksum", BindingFlags.NonPublic | BindingFlags.Instance) ??
                           tScanner.GetMethod("calcChecksum", BindingFlags.NonPublic | BindingFlags.Instance);
        if (mCalc == null)
            throw new Exception("Fehler bezüglich der Methode 'CalcChecksum()'.");

        char[] testData = new[] { '1', '4', '_', '2', '3', '.', '5' };
        char expectedXor = (char)(testData[0] ^ testData[1] ^ testData[2] ^ testData[3] ^ testData[4] ^ testData[5] ^
                                  testData[6]);
        char actualXor = (char)mCalc.Invoke(scannerObj, new object[] { testData });

        if (expectedXor != actualXor)
            throw new Exception("Die Methode 'CalcChecksum' funktioniert nicht wie erwartet.");

        // 3. test: controller dynamic loop test
        ConstructorInfo ctorController = tController.GetConstructor(new[] { typeof(string), tVerwaltung, tSchleuse });
        if (ctorController == null)
            throw new Exception("Invalider Konstruktor des ScannerController.");

        object verwaltungObj = Activator.CreateInstance(tVerwaltung);
        object schleuseObj = Activator.CreateInstance(tSchleuse, 1);
        object controllerObj = ctorController.Invoke(new[] { "COM1", verwaltungObj, schleuseObj });

        FieldInfo fScannerCtrl = tController.GetField("scanner", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fScannerCtrl == null || fScannerCtrl.GetValue(controllerObj) == null)
            throw new Exception("Invalider Konstruktor des Controllers.");

        object scannerInst = fScannerCtrl.GetValue(controllerObj);
        FieldInfo fScannerSerial = tScanner.GetField("serial", BindingFlags.NonPublic | BindingFlags.Instance);
        object serialInst = fScannerSerial.GetValue(scannerInst);
        MethodInfo mSetBytes = tSerial.GetMethod("SetTestBytes");

        MethodInfo mReadBarcode = tScanner.GetMethod("ReadBarcode") ?? tScanner.GetMethod("readBarcode");
        if (mReadBarcode == null) throw new Exception("Die Methode 'ReadBarcode()' fehlt.");

        // invalid checksum
        List<int> invalidPacket = new List<int> { 0x02 };
        foreach (char c in testData) invalidPacket.Add(c);
        invalidPacket.Add((char)(expectedXor + 1));
        invalidPacket.Add(0x03);

        mSetBytes.Invoke(serialInst, new object[] { invalidPacket.ToArray() });
        try
        {
            string resultInvalid = (string)InvokeWithTimeout(mReadBarcode, scannerInst, null, 1000);
            if (!string.IsNullOrEmpty(resultInvalid))
                throw new Exception("Fehler in 'ReadBarcode()'.");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("Zeitüberschreitung") || ex is TimeoutException)
                throw new Exception("Laufzeitfehler: Mögliche Endlosschleife in 'ReadBarcode()'.");
            throw;
        }

        // mock payload: <STX>14_23.5<ETX> 
        List<int> packet = new List<int> { 0x02 };
        foreach (char c in testData) packet.Add(c);
        packet.Add(expectedXor);
        packet.Add(0x03);

        MethodInfo mRun = tController.GetMethod("Run") ?? tController.GetMethod("run");

        // --- scenario a: accepted passenger ---
        mSetBytes.Invoke(serialInst, new object[] { packet.ToArray() });
        PropertyInfo pNextCheck = tVerwaltung.GetProperty("NextCheckResult");
        pNextCheck.SetValue(verwaltungObj, true); // boarding ok

        try
        {
            mRun.Invoke(controllerObj, null);
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Laufzeitfehler bei gültigem Scan in Run(): {ex.InnerException?.Message ?? ex.Message}");
        }

        PropertyInfo pLastPassID = tVerwaltung.GetProperty("LastPassID");
        PropertyInfo pLastGewicht = tVerwaltung.GetProperty("LastGewicht");
        int lastPassId = (int)pLastPassID.GetValue(verwaltungObj);
        double lastGewicht = (double)pLastGewicht.GetValue(verwaltungObj);

        if (lastPassId != 14 || Math.Abs(lastGewicht - 23.5) > 0.01)
            throw new Exception(
                $"CheckBoarding Fehlverhalten. Erwartet: ID 14 und Gewicht 23.5. Erhalten: {lastPassId} und {lastGewicht}");

        PropertyInfo pUnlockCount = tSchleuse.GetProperty("UnlockCount");
        PropertyInfo pLockCount = tSchleuse.GetProperty("LockCount");
        if ((int)pUnlockCount.GetValue(schleuseObj) == 0 || (int)pLockCount.GetValue(schleuseObj) == 0)
            throw new Exception("Fehler mit dem Schließen bzw. Öffnen der Schleuse.");

        // --- scenario b: rejected passenger ---
        mSetBytes.Invoke(serialInst, new object[] { packet.ToArray() }); // data again in buffer
        pNextCheck.SetValue(verwaltungObj, false); // boarding fail

        try
        {
            mRun.Invoke(controllerObj, null);
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Laufzeitfehler bei abgewiesenem Scan in Run(): {ex.InnerException?.Message ?? ex.Message}");
        }

        MethodInfo mGetLogs = tVerwaltung.GetMethod("GetLogs");
        IList logs = mGetLogs.Invoke(verwaltungObj, null) as IList;

        if (logs == null || logs.Count == 0)
            throw new Exception("Bei einem abgewiesenen Passagier wurde kein Log-Eintrag zur Verwaltung hinzugefügt.");

        object firstLog = logs[0];
        PropertyInfo pEintrag = firstLog.GetType().GetProperty("Eintrag");
        if (pEintrag == null || (string)pEintrag.GetValue(firstLog) != "Abgewiesen")
            throw new Exception("Der Log-Eintrag hat ein falsches Format.");

        feedback =
            "Hardware-Schnittstelle, Prüfsummen-Berechnung und Sequenzablauf inkl. Logging wurden exakt und einwandfrei umgesetzt.";
        return true;
    }

    private static bool TestLevel26(Assembly assembly, string sourceCode, out string feedback)
    {
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        Type tVerwaltung = assembly.GetType("FlughafenVerwaltung");
        Type tPassagier = assembly.GetType("Passagier");
        Type tFlug = assembly.GetType("Flug");

        if (tVerwaltung == null || tPassagier == null || tFlug == null)
            throw new Exception("Notwendige Klassen zur Prüfung fehlen.");

        MethodInfo mVerarbeite = tVerwaltung.GetMethod("VerarbeiteGepaeckString") ??
                                 tVerwaltung.GetMethod("verarbeiteGepaeckString");
        if (mVerarbeite == null)
            throw new Exception("Die Methode verarbeiteGepaeckString() fehlt.");

        object verwaltungObj = Activator.CreateInstance(tVerwaltung);

        // setup internal lists dynamically for testing the logic tree
        ConstructorInfo ctorFlug = tFlug.GetConstructors().FirstOrDefault(c => c.GetParameters().Length >= 6);
        object dateFuture = DateTime.Now.AddDays(5);
        object datePast = DateTime.Now.AddDays(-1);

        // check if user implemented LocalDate instead of DateTime
        ParameterInfo[] flugParams = ctorFlug.GetParameters();
        if (flugParams.Length >= 6 && flugParams[2].ParameterType.Name == "LocalDate")
        {
            Type tLocalDate = assembly.GetType("LocalDate");
            if (tLocalDate != null)
            {
                object nowObj = tLocalDate.GetMethod("Now").Invoke(null, null);
                MethodInfo mPlusDays = tLocalDate.GetMethod("PlusDays");
                dateFuture = mPlusDays.Invoke(nowObj, new object[] { 5L });
                datePast = mPlusDays.Invoke(nowObj, new object[] { -1L });
            }
        }

        object flugObjFuture = ctorFlug.Invoke(new[] { "FL1", "NYC", dateFuture, "G1", 100, 200.0 });
        object flugObjPast = ctorFlug.Invoke(new[] { "FL2", "LON", datePast, "G2", 100, 100.0 });

        ConstructorInfo ctorPassagier = tPassagier.GetConstructor(new[] { typeof(string), typeof(string), tFlug });
        object passFuture = ctorPassagier.Invoke(new[] { "Test 1", "T1", flugObjFuture });
        object passPast = ctorPassagier.Invoke(new[] { "Test 2", "T2", flugObjPast });

        FieldInfo fPassId = tPassagier.GetField("passagierID", BindingFlags.NonPublic | BindingFlags.Instance);
        int idFuture = (int)fPassId.GetValue(passFuture);
        int idPast = (int)fPassId.GetValue(passPast);

        FieldInfo fPassagiere = tVerwaltung.GetField("passagiere", BindingFlags.NonPublic | BindingFlags.Instance);
        if (fPassagiere != null)
        {
            IList list = fPassagiere.GetValue(verwaltungObj) as IList;
            if (list == null)
            {
                Type listType = typeof(List<>).MakeGenericType(tPassagier);
                list = (IList)Activator.CreateInstance(listType);
                fPassagiere.SetValue(verwaltungObj, list);
            }

            list.Add(passFuture);
            list.Add(passPast);
        }

        // run logic test paths
        try
        {
            int res1 = (int)mVerarbeite.Invoke(verwaltungObj, new object[] { "INVALID;DATA" });
            if (res1 != -1)
                throw new Exception(
                    "Die Validierung der Parameterlänge oder des Formats funktioniert nicht wie erwartet.");

            int res2 = (int)mVerarbeite.Invoke(verwaltungObj, new object[] { "XYZ;" + idFuture + ";10.0" });
            if (res2 != -1) throw new Exception("Die Überprüfung des Protokoll-Headers schlägt fehl.");

            int res3 = (int)mVerarbeite.Invoke(verwaltungObj, new object[] { "BGG;9999;10.0" });
            if (res3 != -2) throw new Exception("Die Fehlerbehandlung für nicht gefundene Passagiere ist inkorrekt.");

            int res4 = (int)mVerarbeite.Invoke(verwaltungObj, new object[] { "BGG;" + idPast + ";10.0" });
            if (res4 != -3)
                throw new Exception("Die Datumsberechnung (Tage bis Abflug) funktioniert nicht wie erwartet.");

            int res5 = (int)mVerarbeite.Invoke(verwaltungObj, new object[] { "BGG;" + idFuture + ";15.5" });
            if (res5 != 0) throw new Exception("Die Zuschlagsberechnung für reguläres Gepäck (<= 20kg) ist inkorrekt.");

            // expecting: (25.5 - 20) * 15 = 5.5 * 15 = 82.5 -> cast to int yields 82
            int res6 = (int)mVerarbeite.Invoke(verwaltungObj, new object[] { "BGG;" + idFuture + ";25.5" });
            if (res6 != 82)
                throw new Exception(
                    "Die Berechnung des Gepäckzuschlags bei Übergewicht liefert ein falsches Ergebnis.");
        }
        catch (TargetInvocationException ex)
        {
            throw new Exception(
                $"Laufzeitfehler bei der Ausführung der Methode verarbeiteGepaeckString(): {ex.InnerException?.Message}");
        }

        feedback =
            "Generalprobe bestanden! Das Nassi-Shneiderman-Diagramm wurde fehlerfrei und algorithmisch korrekt implementiert.";
        return true;
    }
}