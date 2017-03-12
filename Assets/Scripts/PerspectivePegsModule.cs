using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PerspectivePegsModule : MonoBehaviour
{
    public GameObject Board;

    public KMBombInfo BombInfo;
    public KMBombModule BombModule;
    public KMAudio KMAudio;

    public KMSelectable Peg0;
    public KMSelectable Peg1;
    public KMSelectable Peg2;
    public KMSelectable Peg3;
    public KMSelectable Peg4;

    public Material MatRed;
    public Material MatYellow;
    public Material MatGreen;
    public Material MatBlue;
    public Material MatPurple;

    private const int MATCH_NO = 0;
    private const int MATCH_MAYBE = 1;
    private const int MATCH_YES = 2;
    private const int MATCH_MULTI = 3;

    private bool isComplete = false;

    private MeshRenderer[,] ColourMeshes;
    private KMSelectable[] Pegs;
    private Material[] Mats;

    private bool[] IsMoving = { false, false, false, false, false };
    private bool[] IsUp = { false, false, false, false, false };
    private float[] Lerps = { 0, 0, 0, 0, 0 };

    private Vector3 UP_POS = new Vector3(0, 0, 0.25f);
    private Vector3 DOWN_POS = new Vector3(0, 0, -1.5f);
    private const float DURATION = 0.76f;

    private List<int> CorrectSequence = new List<int> { 0, 0, 0 };
    private List<int> EnteredSequence = new List<int> { 0, 0, 0 };
    private int NextPosition = 0;
    private bool IsPalindrome = false;

    private int moduleId;
    private static int moduleIdCounter = 1;

    void Start()
    {
        moduleId = moduleIdCounter++;

        ColourMeshes = new MeshRenderer[5, 5];

        // Populate the grid
        for (int x = 0; x < 5; x++)
        {
            GameObject quad = Board.transform.Find("Thing" + x).Find("Peg" + x).Find("Quads").gameObject;
            for (int y = 0; y < 5; y++)
            {
                string s = "Colour" + y;
                Transform tr = quad.transform.FindChild(s);
                GameObject go = tr.gameObject;
                ColourMeshes[x, y] = go.GetComponent<MeshRenderer>();
            }
        }

        Pegs = new KMSelectable[] { Peg0, Peg1, Peg2, Peg3, Peg4 };
        Mats = new Material[] { MatRed, MatYellow, MatGreen, MatBlue, MatPurple };

        Peg0.OnInteract += delegate () { HandlePress(0); return false; };
        Peg1.OnInteract += delegate () { HandlePress(1); return false; };
        Peg2.OnInteract += delegate () { HandlePress(2); return false; };
        Peg3.OnInteract += delegate () { HandlePress(3); return false; };
        Peg4.OnInteract += delegate () { HandlePress(4); return false; };

        BombModule.OnActivate += OnActivate;
    }

    string ColoursToString(string s)
    {
        List<int> list = new List<int>();

        foreach (char c in s)
        {
            if (c >= '0' && c <= '4')
            {
                list.Add(c - '0');
            }
            else
            {
                list.Add(-1);
            }
        }

        return ColoursToString(list);
    }

    string ColoursToString(List<int> list)
    {

        string[] colourNames = new string[] { "R", "Y", "G", "B", "P" };
        string str = "";

        foreach (int i in list)
        {
            if (i >= 0)
            {
                str += colourNames[i];
            }
            else
            {
                str += "-";
            }
        }

        return str;
    }

    string GetView(List<Peg> pegs, int view)
    {

        string viewString = "";
        List<Peg> pegView = new List<Peg> {
            pegs [(view + 1) % 5],
            pegs [(view + 2) % 5],
            pegs [(view) % 5],
            pegs [(view + 3) % 5],
            pegs [(view + 4) % 5]
        };

        foreach (Peg p in pegView)
        {
            if (p.colours[view] >= 0)
            {
                viewString += p.colours[view];
            }
            else
            {
                viewString += "-";
            }
        }

        return viewString;
    }

    void SetView(List<Peg> pegs, string viewStr, int view)
    {

        List<Peg> pegView = new List<Peg> {
            pegs [(view + 1) % 5],
            pegs [(view + 2) % 5],
            pegs [(view) % 5],
            pegs [(view + 3) % 5],
            pegs [(view + 4) % 5]
        };

        for (int i = 0; i < 5; i++)
        {
            pegView[i].colours[view] = viewStr[i] - '0';
        }
    }

    // Check the matching potential of a pattern vs a field
    // The field can have non-numeric digits as wildcards
    // Pattern should be 3 chars, field should be 5 chars
    int CheckMatch(string pattern, string field)
    {
        int[] forward = new int[3];
        int[] reverse = new int[3];

        for (int i = 0; i < 3; i++)
        {
            forward[i] = CheckSingleMatch(pattern, field, i, false);
            reverse[i] = CheckSingleMatch(pattern, field, i, true);
        }

        int matches = 0;
        bool wild = false;

        for (int i = 0; i < 3; i++)
        {
            if (forward[i] == MATCH_YES || reverse[i] == MATCH_YES)
            {
                matches++;
            }
            else if (forward[i] == MATCH_MAYBE || reverse[i] == MATCH_MAYBE)
            {
                wild = true;
            }
        }

        if (matches == 1)
            return MATCH_YES;
        else if (matches > 1)
            return MATCH_MULTI;
        else if (wild)
            return MATCH_MAYBE;
        else
            return MATCH_NO;
    }

    int CheckSingleMatch(string pattern, string field, int pos, bool rev)
    {
        bool failed = false;
        bool wild = false;
        for (int i = 0; i < 3; i++)
        {
            int j = i;
            if (rev)
            {
                j = 2 - i;
            }

            char c = field[pos + i];
            if (c >= '0' && c <= '4')
            {
                if (c != pattern[j])
                {
                    failed = true;
                    break;
                }
            }
            else
            {
                wild = true;
            }
        }

        if (failed)
        {
            return MATCH_NO;
        }
        else if (wild)
        {
            return MATCH_MAYBE;
        }
        else
        {
            return MATCH_YES;
        }
    }

    string FillView(string part, string prime, bool isChosen, int view)
    {
        int[] forward = new int[3];
        int[] reverse = new int[3];

        List<int> matches = new List<int>();
        bool hasYes = false;

        for (int i = 0; i < 3; i++)
        {
            forward[i] = CheckSingleMatch(prime, part, i, false);
            reverse[i] = CheckSingleMatch(prime, part, i, true);

            if (forward[i] == MATCH_YES || reverse[i] == MATCH_YES)
            {
                hasYes = true;
            }
            else if (forward[i] == MATCH_MAYBE)
            {
                matches.Add(i);
            }
            else if (reverse[i] == MATCH_MAYBE)
            {
                matches.Add(i + 3);
            }
        }

        if (isChosen && !hasYes)
        {
            int finalPos = matches[Random.Range(0, matches.Count)];
            int startPos = finalPos % 3;
            for (int i = 0; i < 3; i++)
            {
                int j;
                if (finalPos < 3)
                {
                    j = startPos + i;
                }
                else
                {
                    j = startPos + 2 - i;
                }
                part = ReplaceChar(part, prime[i], j);
            }
        }

        string partCopy = part;
        int result;

        int count = 0;
        do
        {
            part = partCopy;
            if (count++ >= 10)
            {
                return null;
            }
            for (int i = 0; i < 5; i++)
            {
                if (part[i] < '0' || part[i] > '4')
                {
                    part = ReplaceChar(part, (char) ('0' + Random.Range(0, 5)), i);
                }
            }
            result = CheckMatch(prime, part);
        } while ((isChosen && result != MATCH_YES) || (!isChosen && result != MATCH_NO));

        return part;
    }

    // Return null if unviable, and need to re-roll
    List<Peg> GenerateColours(int chosenColour, List<Permute> permutations)
    {
        // Create the initial peg data
        List<Peg> pegs = new List<Peg> {
            new Peg (0),
            new Peg (1),
            new Peg (2),
            new Peg (3),
            new Peg (4)
        };

        // Shuffle the pegs
        Shuffler.ShuffleList(pegs);

        //Assign outer colours
        for (int i = 0; i < 5; i++)
        {
            if (pegs[i].colours[i] == -1)
            {
                pegs[i].colours[i] = Random.Range(0, 5);
            }
        }

        // Find matching peg and determine current sequence
        int keyPeg = 0;
        for (int i = 0; i < 5; i++)
        {
            if (pegs[i].prime == chosenColour)
            {
                keyPeg = i;
                break;
            }
        }

        string currentSequence = "";

        for (int i = 0; i < 5; i++)
        {
            int j = (i + keyPeg) % 5;
            currentSequence += pegs[j].colours[j];
        }

        var output = ColoursToString(currentSequence);
        var numSubst = 0;

        // Perform permutations
        foreach (Permute p in permutations)
        {
            string oldSequence = currentSequence;
            currentSequence = p.DoPermutation(currentSequence);

            if (!oldSequence.Equals(currentSequence))
            {
                output += " → " + ColoursToString(currentSequence);
                numSubst++;
            }
        }

        string primeSequence = currentSequence.Substring(0, 3);

        // Try to place the sequence
        bool found = false;
        List<int> positions = new List<int>();
        List<string> views = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            views.Add(GetView(pegs, i));
        }

        for (int i = 0; i < 5; i++)
        {
            int match = CheckMatch(primeSequence, views[i]);

            switch (match)
            {
                case MATCH_MULTI:
                    return null;
                case MATCH_YES:
                    if (found)
                    {
                        return null;
                    }
                    else
                    {
                        found = true;
                        positions = new List<int> { i };
                    }
                    break;
                case MATCH_MAYBE:
                    if (!found)
                    {
                        positions.Add(i);
                    }
                    break;
            }
        }

        if (positions.Count == 0)
        {
            return null;
        }

        int cpos = positions[Random.Range(0, positions.Count)];

        for (int i = 0; i < 5; i++)
        {
            views[i] = FillView(views[i], primeSequence, i == cpos, i);
            if (views[i] == null)
            {
                return null;
            }

            // If this is the chosen view, set the correct sequence
            if (i == cpos)
            {
                string chosenView = views[cpos];
                for (int j = 0; j < 3; j++)
                {
                    if (CheckSingleMatch(primeSequence, chosenView, j, false) == MATCH_YES)
                    {
                        CorrectSequence[0] = (i + ViewToPegSpace(j)) % 5;
                        CorrectSequence[1] = (i + ViewToPegSpace(j + 1)) % 5;
                        CorrectSequence[2] = (i + ViewToPegSpace(j + 2)) % 5;
                        break;
                    }
                    else if (CheckSingleMatch(primeSequence, chosenView, j, true) == MATCH_YES)
                    {
                        CorrectSequence[0] = (i + ViewToPegSpace(j + 2)) % 5;
                        CorrectSequence[1] = (i + ViewToPegSpace(j + 1)) % 5;
                        CorrectSequence[2] = (i + ViewToPegSpace(j)) % 5;
                        break;
                    }
                }
                IsPalindrome = primeSequence[0] == primeSequence[2];
            }
        }

        for (int i = 0; i < 5; i++)
        {
            SetView(pegs, views[i], i);
        }

        Debug.LogFormat("[Perspective Pegs #{0}] Pegs:{1}", moduleId, new string(@"
           _A
         E/00\B
          `..´
   _U     D  C     _F
 Y/44\V          J/11\G
  `..´            `..´
  X  W            I  H 
      _P       _K
    T/33\Q   O/22\L
     `..´     `..´
     S  R     N  M
".Select(ch =>
        {
            if (ch >= '0' && ch <= '4')
                return (ch - '0' == keyPeg) ? '#' : ' ';
            if (ch < 'A' || ch > 'Y')
                return ch;
            var n = ch - 'A';
            return "RYGBP"[pegs[n / 5].colours[n % 5]];
        }).ToArray()));
        Debug.LogFormat("[Perspective Pegs #{0}] Sequence: {1} ({2} substitutions)", moduleId, output, numSubst);

        var names = "top|top-right|bottom-right|bottom-left|top-left".Split('|');
        Debug.LogFormat("[Perspective Pegs #{0}] Look from {3}. Correct solution: {1}{2}",
            moduleId,
            string.Join(", ", CorrectSequence.Select(i => names[i]).ToArray()),
            !IsPalindrome ? null : " or " + string.Join(", ", CorrectSequence.Select(i => names[i]).Reverse().ToArray()),
            names[cpos]);

        return pegs;
    }

    void OnActivate()
    {
        List<List<Permute>> allPermutations = new List<List<Permute>> {

            new List<Permute> {
                new Permute ("011", "341"),
                new Permute ("142", "430"),
                new Permute ("024", "320"),
                new Permute ("132", "311"),
                new Permute ("440", "014"),
                new Permute ("323", "412"),//12
				new Permute ("123", "241"),
                new Permute ("422", "210")
            },

            new List<Permute> {
                new Permute ("343", "132"),
                new Permute ("114", "304"),//04
				new Permute ("203", "143"),
                new Permute ("041", "232"),
                new Permute ("122", "430"),//43
				new Permute ("243", "121"),
                new Permute ("404", "332"),
                new Permute ("010", "043")
            },

            new List<Permute> {
                new Permute ("413", "023"),
                new Permute ("104", "010"),
                new Permute ("210", "234"),
                new Permute ("312", "420"),//42
				new Permute ("041", "213"),//21
				new Permute ("442", "430"),
                new Permute ("011", "330"),
                new Permute ("124", "411")
            }
        };

        string serialNum = BombInfo.GetSerialNumber();
        int batteryCount = BombInfo.GetBatteryCount();

        // Determine chosen colour
        string output = "";

        int diffSum = 0;
        char prevChar = ' ';
        foreach (char c in serialNum)
        {
            if (c >= 'A' && c <= 'Z')
            {
                if (prevChar == ' ')
                {
                    prevChar = c;
                }
                else
                {
                    int diff = Mathf.Abs(prevChar - c);
                    diffSum += diff;
                    output += prevChar + " vs " + c + " = " + diff + ", ";
                    prevChar = ' ';
                }
            }
        }

        int chosenColour = 0;
        switch (diffSum % 10)
        {
            case 0:
            case 3:
                chosenColour = 0;
                break;
            case 4:
            case 9:
                chosenColour = 1;
                break;
            case 1:
            case 7:
                chosenColour = 2;
                break;
            case 5:
            case 8:
                chosenColour = 3;
                break;
            case 2:
            case 6:
                chosenColour = 4;
                break;
        }

        Debug.LogFormat("[Perspective Pegs #{0}] Determining first colour: {1}sum = {2} ({3})", moduleId, output, diffSum, new string[] { "red", "yellow", "green", "blue", "purple" }[chosenColour]);

        List<Permute> permutations;

        switch (batteryCount)
        {
            case 1:
            case 2:
                permutations = allPermutations[0];
                break;
            case 3:
            case 4:
                permutations = allPermutations[1];
                break;
            default:
                permutations = allPermutations[2];
                break;
        }

        List<Peg> result = null;

        do
            result = GenerateColours(chosenColour, permutations);
        while (result == null);

        MoveAllUp();

        // Set colours
        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                ColourMeshes[x, y].material = Mats[result[x].colours[y]];
            }
        }
    }

    protected bool HandlePress(int i)
    {
        if (i >= 0 && i < 5)
        {
            if (!IsMoving[i] && IsUp[i])
            {
                KMAudio.PlaySoundAtTransform("piston_down", this.transform);
                IsMoving[i] = true;
                Lerps[i] = 1;
            }
        }

        return false;
    }

    private void MoveAllUp()
    {
        bool moved = false;
        for (int i = 0; i < 5; i++)
        {
            if (!IsMoving[i] && !IsUp[i])
            {
                IsMoving[i] = true;
                Lerps[i] = 0;
                moved = true;
            }
        }

        if (moved)
        {
            KMAudio.PlaySoundAtTransform("piston_up", this.transform);
        }
    }

    private void MoveAllDown()
    {
        bool moved = false;
        for (int i = 0; i < 5; i++)
        {
            if (!IsMoving[i] && IsUp[i])
            {
                IsMoving[i] = true;
                Lerps[i] = 1;
                moved = true;
            }
        }

        if (moved)
        {
            KMAudio.PlaySoundAtTransform("piston_down", this.transform);
        }
    }

    void Update()
    {

        if (Input.GetKeyDown("1"))
        {
            HandlePress(0);
        }
        if (Input.GetKeyDown("2"))
        {
            HandlePress(1);
        }
        if (Input.GetKeyDown("3"))
        {
            HandlePress(2);
        }
        if (Input.GetKeyDown("4"))
        {
            HandlePress(3);
        }
        if (Input.GetKeyDown("5"))
        {
            HandlePress(4);
        }

        for (int i = 0; i < 5; i++)
        {
            if (IsMoving[i])
            {
                if (IsUp[i])
                {

                    // Move pegs down
                    Lerps[i] -= Time.deltaTime / DURATION;
                    if (Lerps[i] <= 0)
                    {
                        Lerps[i] = 0;
                        IsUp[i] = false;
                        IsMoving[i] = false;

                        // Next sequence entered
                        if (!isComplete)
                        {
                            if (NextPosition < 3)
                            {
                                EnteredSequence[NextPosition++] = i;
                            }

                            // Check for mocule solve
                            if (NextPosition >= 3)
                            {
                                bool win = true;
                                for (int j = 0; j < 3; j++)
                                {
                                    if (CorrectSequence[j] != EnteredSequence[j])
                                    {
                                        win = false;
                                        j = 3;
                                    }
                                }

                                if (!win && IsPalindrome)
                                {
                                    win = true;
                                    for (int j = 0; j < 3; j++)
                                    {
                                        if (CorrectSequence[2 - j] != EnteredSequence[j])
                                        {
                                            win = false;
                                            j = 3;
                                        }
                                    }
                                }

                                if (win)
                                {
                                    BombModule.HandlePass();
                                    isComplete = true;
                                    MoveAllDown();
                                }
                                else
                                {
                                    BombModule.HandleStrike();
                                    MoveAllUp();
                                }

                                NextPosition = 0;
                            }
                        }
                    }
                }
                else
                {

                    // Move pegs up
                    Lerps[i] += Time.deltaTime / DURATION;
                    if (Lerps[i] >= 1)
                    {
                        Lerps[i] = 1;
                        IsUp[i] = true;
                        IsMoving[i] = false;
                    }
                }

                Pegs[i].transform.localPosition = Vector3.Lerp(DOWN_POS, UP_POS, Lerps[i]);
            }
        }
    }

    public static string ReplaceChar(string s, char c, int pos)
    {
        return s.Substring(0, pos) + c + s.Substring(pos + 1);
    }

    public static int ViewToPegSpace(int i)
    {
        if (i == 2)
        {
            return 0;
        }
        else if (i < 2)
        {
            return i + 1;
        }
        return i;
    }
}

class Peg
{

    public int prime;
    public List<int> colours;

    public Peg(int p)
    {
        prime = p;

        colours = new List<int> { p, p, p, -1, -1 };
        Shuffler.ShuffleList(colours);
    }

    public Peg(Peg p)
    {
        colours = new List<int>();
        colours.AddRange(p.colours);
        prime = p.prime;
    }
}

class Permute
{

    public string prime;
    public string alt;

    public Permute(string p, string a)
    {
        prime = p;
        alt = a;
    }

    public static string Reverse(string s)
    {
        string r = "";
        for (int i = s.Length - 1; i >= 0; i--)
        {
            r += s[i];
        }
        return r;
    }

    public string DoPermutation(string s)
    {
        if (s.Contains(prime))
        {
            return s.Replace(prime, alt);
        }

        string srev = Reverse(s);
        if (srev.Contains(prime))
        {
            return Reverse(srev.Replace(prime, alt));
        }

        return s;
    }
}
