using System;
using System.Reflection;

namespace AbiturEliteCode
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

                switch (levelId)
                {
                    case 1:
                        success = TestLevel1(assembly, out feedback);
                        break;
                    case 2:
                        success = TestLevel2(assembly, out feedback);
                        break;
                    case 3:
                        success = TestLevel3(assembly, out feedback);
                        break;
                    case 4:
                        success = TestLevel4(assembly, out feedback);
                        break;
                    case 5:
                        success = TestLevel5(assembly, out feedback);
                        break;
                    default:
                        throw new Exception($"Keine Tests für Level {levelId} definiert.");
                }

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
            if (mB == null) throw new Exception("Methode Bruellen fehlt. Erstelle: public string Bruellen()");

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

            FieldInfo fList = tG.GetField("bewohner");
            if (fList == null) throw new Exception("Feld 'bewohner' fehlt.");

            MethodInfo mGetAlter = tT.GetMethod("GetAlter");
            if (mGetAlter == null) throw new Exception("Methode 'GetAlter()' fehlt.");

            var listInstance = fList.GetValue(g);
            if (listInstance == null) throw new Exception("Liste 'bewohner' ist null.");

            MethodInfo listAdd = listInstance.GetType().GetMethod("Add");

            listAdd.Invoke(listInstance, new object[] { t1 });
            listAdd.Invoke(listInstance, new object[] { t2 });
            listAdd.Invoke(listInstance, new object[] { t3 });

            MethodInfo mAlgo = tG.GetMethod("ErmittleAeltestes");
            if (mAlgo == null) throw new Exception("Methode 'ErmittleAeltestes' fehlt.");

            if (mAlgo.ReturnType != tT) throw new Exception("Rückgabetyp muss 'Tier' sein.");

            object result = mAlgo.Invoke(g, null);

            if (result == null) throw new Exception("Methode gibt null zurück.");

            int resultAge = (int)mGetAlter.Invoke(result, null);

            if (result == t2 && resultAge == 20)
            {
                feedback = "Mini-Prüfung bestanden! Algorithmus korrekt.";
                return true;
            }
            else
            {
                throw new Exception($"Falsches Tier zurückgegeben (Alter: {resultAge}, erwartet: 20).");
            }
        }
    }
}