using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace AbiturEliteCode.cs
{
    // note: in the RunTests method (in MainWindow.axaml.cs) the handholding with the error explanaitions should decrease the further the user progress to make them do more on their own, if we introduce something (moderate) handholding it accepted, but on the last level of a section (which is always a mini-test) we want to check whether the user can do what they were taught on their own

    public class TestResult
    {
        public bool Success { get; set; }
        public string Feedback { get; set; }
        public Exception Error { get; set; }
    }

    public static class LevelTester
    {
        public static TestResult Run(int levelId, Assembly assembly)
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
                    8 => TestLevel8(assembly, out feedback),
                    9 => TestLevel9(assembly, out feedback),
                    10 => TestLevel10(assembly, out feedback),
                    11 => TestLevel11(assembly, out feedback),
                    12 => TestLevel12(assembly, out feedback),
                    13 => TestLevel13(assembly, out feedback),
                    14 => TestLevel14(assembly, out feedback),
                    _ => throw new Exception($"Keine Tests für Level {levelId} definiert."),
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
            if (tierType == null) throw new Exception("Klasse 'Tier' nicht gefunden. Stelle sicher, dass du 'public class Tier' geschrieben hast.");

            ConstructorInfo ctor = tierType.GetConstructor(new[] { typeof(string), typeof(int) });
            if (ctor == null) throw new Exception("Konstruktor Tier(string, int) fehlt. Füge einen Konstruktor mit zwei Parametern hinzu: public Tier(string name, int alter)");

            object tier = ctor.Invoke(new object[] { "Löwe", 5 });
            FieldInfo fName = tierType.GetField("name", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo fAlter = tierType.GetField("alter", BindingFlags.NonPublic | BindingFlags.Instance);

            if (fName == null) throw new Exception("Feld 'name' fehlt oder ist nicht private. Füge hinzu: private string name;");
            if (fAlter == null) throw new Exception("Feld 'alter' fehlt oder ist nicht private. Füge hinzu: private int alter;");

            string actualName = (string)fName.GetValue(tier);
            int actualAlter = (int)fAlter.GetValue(tier);

            if (actualName == "Löwe" && actualAlter == 5)
            {
                feedback = "Klasse Tier korrekt implementiert! Felder und Konstruktor funktionieren.";
                return true;
            }
            else
            {
                throw new Exception("Konstruktor setzt die Werte nicht korrekt. Im Konstruktor: this.name = name; und this.alter = alter;");
            }
        }

        private static bool TestLevel2(Assembly assembly, out string feedback)
        {
            Type t = assembly.GetType("Tier");
            if (t == null) throw new Exception("Klasse 'Tier' nicht gefunden. Hast du sie gelöscht?");

            object obj = Activator.CreateInstance(t);

            MethodInfo mSet = t.GetMethod("SetAlter");
            MethodInfo mGet = t.GetMethod("GetAlter");

            if (mSet == null)
            {
                if (t.GetMethod("setAlter") != null)
                    throw new Exception("Du hast 'setAlter' (Java-Stil) verwendet. In C# nutzen wir PascalCase: 'SetAlter'.");
                throw new Exception("Methode SetAlter fehlt. Erstelle: public void SetAlter(int neuesAlter)");
            }
            if (mGet == null)
            {
                if (t.GetMethod("getAlter") != null)
                    throw new Exception("Du hast 'getAlter' (Java-Stil) verwendet. In C# nutzen wir PascalCase: 'GetAlter'.");
                throw new Exception("Methode GetAlter fehlt. Erstelle: public int GetAlter()");
            }
            FieldInfo fAlter = t.GetField("alter", BindingFlags.NonPublic | BindingFlags.Instance);

            if (mSet == null) throw new Exception("Methode SetAlter fehlt. Erstelle: public void SetAlter(int neuesAlter)");
            if (mGet == null) throw new Exception("Methode GetAlter fehlt. Erstelle: public int GetAlter()");

            // initial value check
            fAlter.SetValue(obj, 10);

            // test 1: invalid (lower)
            mSet.Invoke(obj, new object[] { 5 });
            int val1 = (int)mGet.Invoke(obj, null);
            if (val1 != 10) throw new Exception("Fehler: Alter wurde trotz kleinerem Wert geändert! SetAlter muss prüfen: if (neuesAlter > alter)");

            // test 2: valid (higher)
            mSet.Invoke(obj, new object[] { 12 });
            int val2 = (int)mGet.Invoke(obj, null);
            if (val2 != 12) throw new Exception("Fehler: Alter wurde trotz gültigem Wert nicht geändert. Setze alter = neuesAlter wenn die Bedingung erfüllt ist.");

            feedback = "Kapselung und Validierung erfolgreich implementiert!";
            return true;
        }

        private static bool TestLevel3(Assembly assembly, out string feedback)
        {
            Type tTier = assembly.GetType("Tier");
            Type tLoewe = assembly.GetType("Loewe");

            if (tTier == null) throw new Exception("Klasse Tier fehlt. Erstelle: public abstract class Tier");
            if (tLoewe == null) throw new Exception("Klasse Loewe fehlt. Erstelle: public class Loewe : Tier");

            if (!tTier.IsAbstract) throw new Exception("Klasse Tier muss 'abstract' sein. Schreibe: public abstract class Tier");
            if (!tLoewe.IsSubclassOf(tTier)) throw new Exception("Loewe erbt nicht von Tier. Füge hinzu: public class Loewe : Tier");

            // check constructor chaining
            ConstructorInfo ctor = tLoewe.GetConstructor(new[] { typeof(string), typeof(int) });
            if (ctor == null) throw new Exception("Konstruktor Loewe(string, int) fehlt. Erstelle: public Loewe(string name, int laenge) : base(name)");

            // we cannot instantiate Tier, but we can instantiate Loewe
            object leo = ctor.Invoke(new object[] { "Simba", 50 });

            // check Bruellen
            MethodInfo mB = tLoewe.GetMethod("Bruellen");

            if (mB == null)
            {
                if (tLoewe.GetMethod("bruellen") != null)
                    throw new Exception("Du hast 'bruellen' (Java-Stil) verwendet. In C# schreiben wir Methoden groß: 'Bruellen' (in den nächsten Levels wird dieser Fehler nicht erneut explizit erwähnt).");
                throw new Exception("Methode Bruellen fehlt. Erstelle: public string Bruellen()");
            }

            string sound = (string)mB.Invoke(leo, null);
            if (string.IsNullOrEmpty(sound)) throw new Exception("Bruellen gibt nichts zurück. Die Methode sollte einen String zurückgeben.");

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
            if (tTier.IsAbstract)
            {
                throw new Exception("Für dieses Level muss Klasse 'Tier' konkret (nicht abstract) sein.");
            }

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
            MethodInfo mCount = tG.GetMethod("AnzahlTiere");

            if (mAdd == null) throw new Exception("Methode 'Hinzufuegen' nicht gefunden.");
            if (mCount == null) throw new Exception("Methode 'AnzahlTiere' nicht gefunden.");

            // check method signatures
            var addParams = mAdd.GetParameters();
            if (addParams.Length != 1 || addParams[0].ParameterType != tTier)
            {
                throw new Exception("Methode Hinzufuegen muss Parameter vom Typ Tier haben.");
            }

            if (mCount.ReturnType != typeof(int))
            {
                throw new Exception("Methode AnzahlTiere muss int zurückgeben.");
            }

            // create Tier instance
            object animal;
            try
            {
                animal = Activator.CreateInstance(tTier);
            }
            catch
            {
                throw new Exception("Tier konnte nicht instanziiert werden. Tier braucht einen Konstruktor ohne Parameter.");
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
            {
                throw new Exception($"AnzahlTiere sollte initial 0 sein, ist aber {initialCount}. Initialisiere die Liste im Konstruktor.");
            }

            // add one animal
            try
            {
                mAdd.Invoke(g, new object[] { animal });
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
            else
            {
                throw new Exception($"AnzahlTiere gibt {countAfterAdd} statt 1 zurück. Prüfe die Liste.");
            }
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
                return Activator.CreateInstance(tT, new object[] { age });
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

            listAdd.Invoke(listInstance, new object[] { t1 });
            listAdd.Invoke(listInstance, new object[] { t2 });
            listAdd.Invoke(listInstance, new object[] { t3 });

            MethodInfo mAlgo = tG.GetMethod("ErmittleAeltestes");
            if (mAlgo == null) throw new Exception("Methode 'ErmittleAeltestes' fehlt.");

            if (mAlgo.ReturnType != tT) throw new Exception("Rückgabetyp muss 'Tier' sein.");

            object result = mAlgo.Invoke(g, null);

            if (result == null) throw new Exception("Methode 'ErmittleAeltestes' hat 'null' zurückgegeben.");

            MethodInfo mGetAlter = tT.GetMethod("GetAlter");
            if (mGetAlter == null) throw new Exception("Methode 'GetAlter()' fehlt.");

            int resultAge = (int)mGetAlter.Invoke(result, null);

            if (resultAge == 20)
            {
                feedback = "Mini-Prüfung bestanden! Algorithmus korrekt.";
                return true;
            }
            else
            {
                throw new Exception($"Falsches Tier zurückgegeben (Alter: {resultAge}, erwartet: 20).");
            }
        }

        private static object SafeCreatePaket(Type tPaket, string ort, double gewicht)
        {
            // try finding correct constructor: (string, double)
            ConstructorInfo ctorCorrect = tPaket.GetConstructor(new[] { typeof(string), typeof(double) });
            if (ctorCorrect != null)
            {
                return ctorCorrect.Invoke(new object[] { ort, gewicht });
            }

            // try finding reversed constructor: (double, string) -> common mistake
            ConstructorInfo ctorReversed = tPaket.GetConstructor(new[] { typeof(double), typeof(string) });
            if (ctorReversed != null)
            {
                throw new Exception("Fehler im Konstruktor von 'Paket': Die Reihenfolge der Parameter ist falsch.\nErwartet: Paket(string ziel, double gewicht)\nGefunden: Paket(double gewicht, string ziel)\nBitte passen Sie die Reihenfolge an das Diagramm an.");
            }

            throw new Exception("Konstruktor für 'Paket' nicht gefunden. Erwartet: public Paket(string ziel, double gewicht).");
        }

        private static bool TestLevel6(Assembly assembly, out string feedback)
        {
            Type tPaket = assembly.GetType("Paket");
            if (tPaket == null) throw new Exception("Klasse 'Paket' nicht gefunden.");

            Type tLager = assembly.GetType("Lager");
            if (tLager == null) throw new Exception("Klasse 'Lager' nicht gefunden.");

            MethodInfo mAdd = tLager.GetMethod("Hinzufuegen");
            if (mAdd == null) throw new Exception("Methode 'Hinzufuegen' fehlt in der Klasse Lager.");

            MethodInfo mErmittle = tLager.GetMethod("ErmittleLeichtestes");
            if (mErmittle == null)
            {
                if (tLager.GetMethod("ErmittleSchwerstes") != null)
                    throw new Exception("Achtung: Die Aufgabe wurde geändert! Wir suchen nun das *leichteste* Paket. Bitte benennen Sie die Methode 'ErmittleLeichtestes'.");

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
                mAdd.Invoke(lager, new object[] { SafeCreatePaket(tPaket, "Berlin", 50.0) });
                mAdd.Invoke(lager, new object[] { SafeCreatePaket(tPaket, "Hamburg", 5.5) }); // The lightest
                mAdd.Invoke(lager, new object[] { SafeCreatePaket(tPaket, "München", 10.0) });
            }
            catch (TargetInvocationException ex)
            {
                throw new Exception($"Fehler beim Hinzufügen von Paketen: {ex.InnerException?.Message ?? ex.Message}");
            }

            object result = mErmittle.Invoke(lager, null);

            if (result == null) throw new Exception("ErmittleLeichtestes() liefert 'null', obwohl Pakete im Lager sind.");

            MethodInfo mGetW = tPaket.GetMethod("GetGewicht");
            if (mGetW == null) throw new Exception("Methode 'GetGewicht()' fehlt in Klasse Paket.");

            double resWeight = (double)mGetW.Invoke(result, null);

            if (Math.Abs(resWeight - 5.5) < 0.01)
            {
                feedback = "Algorithmus korrekt implementiert! Das leichteste Paket wurde gefunden.";
                return true;
            }
            throw new Exception($"Falsches Paket ermittelt. Gewicht des zurückgegebenen Pakets: {resWeight}, Erwartet: 5.5 (das Leichteste).");
        }

        private static bool TestLevel7(Assembly assembly, out string feedback)
        {
            Type tLager = assembly.GetType("Lager");
            Type tPaket = assembly.GetType("Paket");
            if (tLager == null || tPaket == null) throw new Exception("Klasse Lager oder Paket fehlt.");

            object lager = Activator.CreateInstance(tLager);
            MethodInfo mAdd = tLager.GetMethod("Hinzufuegen");
            MethodInfo mFilter = tLager.GetMethod("FilterePakete");

            if (mAdd == null) throw new Exception("Methode Hinzufuegen fehlt.");
            if (mFilter == null) throw new Exception("Methode FilterePakete fehlt.");

            mAdd.Invoke(lager, new object[] { SafeCreatePaket(tPaket, "Berlin", 15.0) }); // match
            mAdd.Invoke(lager, new object[] { SafeCreatePaket(tPaket, "München", 20.0) }); // wrong city
            mAdd.Invoke(lager, new object[] { SafeCreatePaket(tPaket, "Berlin", 5.0) }); // too light
            mAdd.Invoke(lager, new object[] { SafeCreatePaket(tPaket, "Berlin", 10.0) }); // boundary (not > 10)

            object resObj = mFilter.Invoke(lager, new object[] { "Berlin" });

            IList list = resObj as IList;
            if (list == null) throw new Exception("Rückgabewert ist keine Liste (List<Paket>).");

            if (list.Count == 1)
            {
                feedback = "Filterung erfolgreich! Nur Pakete > 10kg und passender Ort wurden übernommen.";
                return true;
            }

            throw new Exception($"Falsche Anzahl Pakete zurückgegeben. Erwartet: 1, Erhalten: {list.Count}. Prüfen Sie die Bedingungen (ort == ziel && gewicht > 10).");
        }

        private static bool TestLevel8(Assembly assembly, out string feedback)
        {
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

            MethodInfo mSort = tLager.GetMethod("Sortiere");
            if (mSort == null) throw new Exception("Methode 'Sortiere' fehlt.");

            try
            {
                mSort.Invoke(lager, null);
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Ausführen von Sortiere(): " + (ex.InnerException?.Message ?? ex.Message));
            }

            MethodInfo mGetW = tPaket.GetMethod("GetGewicht");
            double w1 = (double)mGetW.Invoke(list[0], null);
            double w2 = (double)mGetW.Invoke(list[1], null);
            double w3 = (double)mGetW.Invoke(list[2], null);

            if (w1 <= w2 && w2 <= w3)
            {
                feedback = "Bubble Sort korrekt implementiert! Die Liste ist aufsteigend sortiert.";
                return true;
            }
            throw new Exception($"Sortierung fehlerhaft. Reihenfolge: {w1}, {w2}, {w3}.");
        }

        private static bool TestLevel9(Assembly assembly, out string feedback)
        {
            Type tKnoten = assembly.GetType("Knoten");
            Type tBand = assembly.GetType("Foerderband");
            Type tPaket = assembly.GetType("Paket");

            if (tKnoten == null) throw new Exception("Klasse 'Knoten' fehlt.");
            if (tBand == null) throw new Exception("Klasse 'Foerderband' fehlt.");

            object band = Activator.CreateInstance(tBand);
            MethodInfo mAnh = tBand.GetMethod("Anhaengen");
            if (mAnh == null) throw new Exception("Methode 'Anhaengen' fehlt.");

            object p1 = SafeCreatePaket(tPaket, "A", 10.0);
            object p2 = SafeCreatePaket(tPaket, "B", 20.0);

            // add 1
            mAnh.Invoke(band, new object[] { p1 });

            FieldInfo fKopf = tBand.GetField("kopf", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fKopf == null) throw new Exception("Feld 'kopf' in Foerderband fehlt.");

            object kopfNode = fKopf.GetValue(band);
            if (kopfNode == null) throw new Exception("Kopf ist null nach dem ersten Einfügen.");

            // check content of head
            FieldInfo fInhalt = tKnoten.GetField("inhalt", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fInhalt == null) throw new Exception("Feld 'inhalt' in Knoten fehlt.");
            if (fInhalt.GetValue(kopfNode) != p1) throw new Exception("Erster Knoten enthält nicht das korrekte Paket.");

            // add 2
            mAnh.Invoke(band, new object[] { p2 });

            // check linking
            FieldInfo fNachfolger = tKnoten.GetField("nachfolger", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fNachfolger == null) throw new Exception("Feld 'nachfolger' in Knoten fehlt.");

            object secondNode = fNachfolger.GetValue(kopfNode);
            if (secondNode == null) throw new Exception("Verkettung fehlerhaft. 'nachfolger' vom Kopf ist null.");

            if (fInhalt.GetValue(secondNode) != p2) throw new Exception("Zweiter Knoten enthält falsches Paket.");

            feedback = "Verkettete Liste funktioniert!";
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

            MethodInfo mTop3 = tLogistik.GetMethod("GetTop3Schwere");
            if (mTop3 == null) throw new Exception("Methode GetTop3Schwere fehlt.");

            object resObj = mTop3.Invoke(zentrum, new object[] { "Berlin" });
            IList resList = resObj as IList;

            if (resList == null) throw new Exception("Keine Liste zurückgegeben.");
            if (resList.Count != 3) throw new Exception($"Liste sollte genau 3 Elemente enthalten, hat aber {resList.Count}.");

            MethodInfo mGetW = tPaket.GetMethod("GetGewicht");
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
            if (fListe == null) throw new Exception("Feld 'schuelerListe' fehlt in Klasse 'Klasse' oder ist nicht private.");
            if (fListe.GetValue(klasseObj) == null) throw new Exception("Die Liste 'schuelerListe' ist null. Sie muss im Konstruktor mit 'new List<Schueler>()' initialisiert werden.");

            MethodInfo mAdd = tKlasse.GetMethod("AddSchueler");
            if (mAdd == null) mAdd = tKlasse.GetMethod("addSchueler");
            if (mAdd == null) throw new Exception("Methode 'AddSchueler' fehlt.");

            MethodInfo mSchnitt = tKlasse.GetMethod("BerechneSchnittBestanden");
            if (mSchnitt == null) throw new Exception("Methode 'BerechneSchnittBestanden' fehlt.");

            object CreateSchueler(int n) => ctorSchueler.Invoke(new object[] { n });

            // logic test
            // scenario: 
            // student a: 15 points (passed)
            // student b: 10 points (passed)
            // student c: 3 points (failed -> should be ignored)
            // student d: 5 points (passed)
            // calculation: (15 + 10 + 5) / 3 = 10.0d
            mAdd.Invoke(klasseObj, new object[] { CreateSchueler(15) });
            mAdd.Invoke(klasseObj, new object[] { CreateSchueler(10) });
            mAdd.Invoke(klasseObj, new object[] { CreateSchueler(3) });
            mAdd.Invoke(klasseObj, new object[] { CreateSchueler(5) });

            double result = (double)mSchnitt.Invoke(klasseObj, null);

            if (Math.Abs(result - 10.0) < 0.01)
            {
                // test edge case: only failures
                object kFail = ctorKlasse.Invoke(new object[] { "Fail-Class" });
                mAdd.Invoke(kFail, new object[] { CreateSchueler(2) });
                double resFail = (double)mSchnitt.Invoke(kFail, null);

                if (Math.Abs(resFail) < 0.01)
                {
                    feedback = "Sehr gut! Der Schnitt wurde korrekt berechnet und nicht bestandene Schüler ignoriert.";
                    return true;
                }
                throw new Exception("Wenn keine Schüler bestanden haben, muss 0.0 zurückgegeben werden (Division durch Null verhindern!).");
            }

            // diagnose error
            if (Math.Abs(result - 8.25) < 0.01) throw new Exception("Fehler: Sie haben alle Schüler (auch die mit Note < 5) mit einberechnet.");
            throw new Exception($"Falsches Ergebnis. Erwartet: 10.0. Erhalten: {result:F2}. Prüfen Sie die Bedingung (Note > 4).");
        }

        private static bool TestLevel12(Assembly assembly, out string feedback)
        {
            Type tSchule = assembly.GetType("Schule");
            Type tLehrer = assembly.GetType("Lehrer");
            Type tKlasse = assembly.GetType("Klasse");

            if (tSchule == null || tLehrer == null || tKlasse == null) throw new Exception("Eine der Klassen (Schule, Lehrer, Klasse) fehlt.");

            object schule = Activator.CreateInstance(tSchule);
            object k1 = Activator.CreateInstance(tKlasse); // dummy classes
            object k2 = Activator.CreateInstance(tKlasse);
            object k3 = Activator.CreateInstance(tKlasse);

            object l1 = Activator.CreateInstance(tLehrer);
            object l2 = Activator.CreateInstance(tLehrer);

            // setup methods
            MethodInfo mAddLehrer = tSchule.GetMethod("AddLehrer");
            MethodInfo mFind = tSchule.GetMethod("FindeVielBeschaeftigte");
            MethodInfo mAddKlasse = tLehrer.GetMethod("AddKlasse");

            if (mAddLehrer == null) throw new Exception("Methode AddLehrer in Schule fehlt.");
            if (mFind == null) throw new Exception("Methode FindeVielBeschaeftigte in Schule fehlt.");
            if (mAddKlasse == null) throw new Exception("Methode AddKlasse in Lehrer fehlt.");

            // scene:
            // lehrer 1 has 3 classes (should be found)
            mAddKlasse.Invoke(l1, new object[] { k1 });
            mAddKlasse.Invoke(l1, new object[] { k2 });
            mAddKlasse.Invoke(l1, new object[] { k3 });

            // lehrer 2 has 2 classes (shouldnt be found, strict > 2)
            mAddKlasse.Invoke(l2, new object[] { k1 });
            mAddKlasse.Invoke(l2, new object[] { k2 });

            mAddLehrer.Invoke(schule, new object[] { l1 });
            mAddLehrer.Invoke(schule, new object[] { l2 });

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

            if (resultList.Count == 2) throw new Exception("Es wurden zu viele Lehrer gefunden. Die Bedingung war 'mehr als 2' (> 2), nicht 'ab 2' (>= 2).");

            throw new Exception($"Falsche Anzahl Ergebnisse. Erwartet: 1, Erhalten: {resultList.Count}.");
        }

        private static bool TestLevel13(Assembly assembly, out string feedback)
        {
            Type tSchueler = assembly.GetType("Schueler");
            Type tFehltag = assembly.GetType("Fehltag");

            if (tSchueler == null || tFehltag == null) throw new Exception("Klassenstruktur unvollständig (Schueler oder Fehltag fehlt).");

            ConstructorInfo ctorFehltag = tFehltag.GetConstructor(new[] { typeof(DateTime), typeof(bool) });
            if (ctorFehltag == null) throw new Exception("Konstruktor Fehltag(DateTime, bool) fehlt.");

            MethodInfo mAdd = tSchueler.GetMethod("AddFehltag");
            if (mAdd == null) throw new Exception("Methode AddFehltag fehlt in Klasse Schueler.");

            MethodInfo mCheck = tSchueler.GetMethod("HatKritischGefehlt");
            if (mCheck == null) throw new Exception("Methode HatKritischGefehlt fehlt.");

            object s = Activator.CreateInstance(tSchueler);

            void AddDay(int daysAgo, bool exc)
            {
                object ft = ctorFehltag.Invoke(new object[] { DateTime.Now.AddDays(-daysAgo), exc });
                mAdd.Invoke(s, new object[] { ft });
            }

            // case 1: no absences
            if ((bool)mCheck.Invoke(s, null)) throw new Exception("Gibt true zurück, obwohl keine Fehltage existieren.");

            // case 2: old unexcused absence (e.g. 40 days ago) -> should be false
            AddDay(40, false);
            if ((bool)mCheck.Invoke(s, null)) throw new Exception("Gibt true zurück, obwohl der unentschuldigte Fehltag länger als 1 Monat zurückliegt.");

            // case 3: recent excused absence -> should still be false
            AddDay(5, true);
            if ((bool)mCheck.Invoke(s, null)) throw new Exception("Gibt true zurück, obwohl der aktuelle Fehltag entschuldigt ist.");

            // case 4: recent unexcused absence -> should be true
            AddDay(10, false);
            if (!(bool)mCheck.Invoke(s, null)) throw new Exception("Gibt false zurück, obwohl ein unentschuldigter Fehltag im letzten Monat existiert.");

            feedback = "Datumslogik korrekt implementiert! Zeitraum und Status wurden richtig geprüft.";
            return true;
        }

        private static bool TestLevel14(Assembly assembly, out string feedback)
        {
            Type tSchule = assembly.GetType("Schule");
            Type tKlasse = assembly.GetType("Klasse");
            Type tSchueler = assembly.GetType("Schueler");

            if (tSchule == null || tKlasse == null || tSchueler == null) throw new Exception("Klassenstruktur fehlt (Schule, Klasse oder Schueler).");

            object schule = Activator.CreateInstance(tSchule);

            // methods setup
            MethodInfo mAddKlasse = tSchule.GetMethod("AddKlasse") ?? tSchule.GetMethod("addKlasse");
            MethodInfo mAddSchueler = tKlasse.GetMethod("AddSchueler") ?? tKlasse.GetMethod("addSchueler");
            MethodInfo mWarn = tSchule.GetMethod("ErstelleWarnungen");

            if (mAddKlasse == null) throw new Exception("AddKlasse fehlt.");
            if (mAddSchueler == null) throw new Exception("AddSchueler fehlt.");
            if (mWarn == null) throw new Exception("ErstelleWarnungen fehlt.");

            // constructors
            ConstructorInfo cKlasse = tKlasse.GetConstructor(new[] { typeof(string) });
            ConstructorInfo cSchueler = tSchueler.GetConstructor(new[] { typeof(string), typeof(int) });

            if (cKlasse == null || cSchueler == null) throw new Exception("Konstruktoren fehlen (Klasse(string) oder Schueler(string, int)).");

            // build hierarchy
            // class 10a
            object k1 = cKlasse.Invoke(new object[] { "10A" });
            object s1 = cSchueler.Invoke(new object[] { "Max", 3 }); // fail
            object s2 = cSchueler.Invoke(new object[] { "Lisa", 10 }); // pass
            mAddSchueler.Invoke(k1, new object[] { s1 });
            mAddSchueler.Invoke(k1, new object[] { s2 });

            // class 11b
            object k2 = cKlasse.Invoke(new object[] { "11B" });
            object s3 = cSchueler.Invoke(new object[] { "Tom", 4 }); // fail
            mAddSchueler.Invoke(k2, new object[] { s3 });

            mAddKlasse.Invoke(schule, new object[] { k1 });
            mAddKlasse.Invoke(schule, new object[] { k2 });

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
    }
}