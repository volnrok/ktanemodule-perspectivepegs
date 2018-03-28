using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class PerspectivePegsModule : MonoBehaviour
{
    public GameObject Board;
    public Transform PegCircle;

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
            GameObject quad = Board.transform.Find("Things").Find("Thing" + x).Find("Peg" + x).Find("Quads").gameObject;
            for (int y = 0; y < 5; y++)
            {
                string s = "Colour" + y;
                Transform tr = quad.transform.Find(s);
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
                                    Debug.LogFormat("[Perspective Pegs #{0}] Module solved.", moduleId);
                                    BombModule.HandlePass();
                                    isComplete = true;
                                    MoveAllDown();
                                }
                                else
                                {
                                    var names = "top|top-right|bottom-right|bottom-left|top-left".Split('|');
                                    Debug.LogFormat("[Perspective Pegs #{0}] You entered: {1}. Strike.", moduleId, string.Join(", ", EnteredSequence.Select(e => names[e]).ToArray()));
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

    private int GetPosition(string position)
    {
        switch (position)
        {
            case "top":
            case "t":
            case "topmiddle":
            case "topcenter":
            case "topcentre":
            case "tm":
            case "tc":
            case "middletop":
            case "middlecenter":
            case "middlecentre":
            case "mt":
            case "mc":
            case "1":
                return 0;

            case "tr":
            case "topright":
            case "righttop":
            case "rt":
            case "2":
                return 1;

            case "br":
            case "bottomright":
            case "rightbottom":
            case "rb":
            case "3":
                return 2;

            case "bl":
            case "bottomleft":
            case "leftbottom":
            case "lb":
            case "4":
                return 3;

            case "tl":
            case "topleft":
            case "lefttop":
            case "lt":
            case "5":
                return 4;
            case "press":
            case "submit":
                return -3;
            case "pegs":
                return -2;
            default:
                return -1;
        }
    }

    private static readonly float[] VIEWS = new float[5]{0f, -72f, -144f, -216f, -288f};

    IEnumerator RotateBomb(bool frontFace, bool viewPegs, int initialCirclePosition)
    {
        yield return null;
        float Angle = -40;
        int rotate = viewPegs ? 180 : 0;
        Vector3 lerp0 = viewPegs ? Vector3.zero : new Vector3(frontFace ? -Angle : Angle, 0, 0);
        Vector3 lerp1 = viewPegs ? new Vector3(frontFace ? -Angle : Angle, 0, 0) : Vector3.zero;
        Quaternion current = PegCircle.localRotation;

        for (float i = 0; i <= 1; i += Time.deltaTime * (TwitchShouldCancelCommand ? 8 : 1))
        {
            PegCircle.localRotation = Quaternion.Lerp(current, Quaternion.Euler(0, Mathf.Round(VIEWS[initialCirclePosition % 5] + rotate), 0), i);
            yield return Quaternion.Euler(Vector3.Lerp(lerp0,lerp1 , i));
            yield return null;
        }
        PegCircle.localRotation = Quaternion.Euler(current.x, Mathf.Round(VIEWS[initialCirclePosition % 5] + rotate), current.z);
        yield return Quaternion.Euler(lerp1);
        yield return null;
    }

    IEnumerator RotatePegCircle(int position)
    {
        yield return null;
        int rotate = 180;
        Quaternion current = PegCircle.localRotation;
        for (float i = 0; i <= 1; i += Time.deltaTime * (TwitchShouldCancelCommand ? 8 : 1))
        {
            PegCircle.localRotation = Quaternion.Lerp(current, Quaternion.Euler(0,Mathf.Round(VIEWS[position % 5] + rotate),0), i);
                yield return null;
        }
        PegCircle.localRotation = Quaternion.Euler(current.x, Mathf.Round(VIEWS[position % 5] + rotate), current.z);
        yield return null;
    }

    IEnumerator RotatePeg(int position, int rotation)
    {
        yield return null;
        Transform peg = Board.transform.Find("Things").Find("Thing" + (position % 5)).Find("Peg" + (position % 5));
        Transform pegbase = Board.transform.Find("Things").Find("Thing" + (position % 5)).Find("Base");
        Quaternion current = peg.localRotation;
        for (float i = 0; i <= 1; i += Time.deltaTime * (TwitchShouldCancelCommand ? 8 : 1))
        {
            peg.localRotation = Quaternion.Lerp(current, Quaternion.Euler(current.x, current.y, Mathf.Round(VIEWS[rotation % 5])), i);
            pegbase.localRotation = Quaternion.Lerp(current, Quaternion.Euler(current.x, current.y, Mathf.Round(VIEWS[rotation % 5])), i);
            yield return null;
        }
        peg.localRotation = Quaternion.Euler(current.x, current.y, Mathf.Round(VIEWS[rotation % 5]));
        pegbase.localRotation = Quaternion.Euler(current.x, current.y, Mathf.Round(VIEWS[rotation % 5]));
        yield return null;

    }

    void TwitchHandleForcedSolve()
    {
        //Move all of the pegs down.
        MoveAllDown();

        //Keep souvenir from processing this module.
        EnteredSequence[0] = -1;
        EnteredSequence[1] = -1;
        EnteredSequence[2] = -1;

        //So that the solution won't be checked.
        isComplete = true;
        Debug.LogFormat("[Perspective Pegs #{0}] Module forcibly solved.", moduleId);
        BombModule.HandlePass();
    }

    private string TwitchHelpMessage = "Look for the peg with specific color using !{0} rotate pegs. Read off the color sequence with !{0} rotate br. Look at the peg lines with !{0} rotate. Look at a specific line with !{0} look bl. Press the pegs with !{0} press bl t br. | Positions in clockwise order are T, TR, BR, BL, TL.";
    private bool TwitchShouldCancelCommand;

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();


        List<string> split = command.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries).ToList();

        if (split[0] == "rotate" || split[0] == "look")
        {
            if (split[0] == "look" && (split.Count == 1 || GetPosition(split[1]) < 0))
            {
                yield return split.Count == 1 
                    ? string.Format("sendtochaterror I don't know which peg I should be looking at.") 
                    : string.Format("sendtochaterror I don't know what you mean by the {0} peg", split[1]);
                yield break;
            }

            if (split.Count > 2)
            {
                yield return string.Format("sendtochaterror I don't know how to process command: {0}", command);
                yield break;
            }

            int start = 0;
            if (split.Count == 2)
                start = GetPosition(split[1]);

            if (start == -1 || start == -3)
            {
                yield return string.Format("sendtochaterror I don't know what you mean by the {0} peg.", split[1]);
                yield break;
            }
                

            bool rotatepegs = start == -2;
            if (rotatepegs)
                start = 0;
            yield return null;
            yield return null;

            bool frontFace = transform.parent.parent.localEulerAngles.z < 45 || transform.parent.parent.localEulerAngles.z > 315;

            IEnumerator rotate = RotateBomb(frontFace, true, start);
            while (rotate.MoveNext())
                yield return rotate.Current;

            if (rotatepegs)
            {
                for (int i = 0; i < 5 && !TwitchShouldCancelCommand; i++)
                {
                    for (int j = 0; j < 5; j++)
                    {
                        IEnumerator rotatepeg = RotatePeg(i, j + 1);
                        while (rotatepeg.MoveNext())
                            yield return rotatepeg.Current;
                    }
                }
            }
            else
            {
                if (split[0] == "rotate")
                {
                    for (int i = start; i < (start + 4); i++)
                    {
                        if(!TwitchShouldCancelCommand)
                            yield return new WaitForSeconds(split.Count == 2 ? 0.25f : 3f);

                        rotate = RotatePegCircle(i + 1);
                        while (rotate.MoveNext())
                            yield return rotate.Current;
                    }
                }
                if (!TwitchShouldCancelCommand)
                {
                    if (split[0] == "look")
                        yield return new WaitForSeconds(4);
                    else
                        yield return new WaitForSeconds(split.Count == 2 ? 0.25f : 3f);
                }
            }

            rotate = RotateBomb(frontFace, false, 0);
            while (rotate.MoveNext())
                yield return rotate.Current;

            if (TwitchShouldCancelCommand)
                yield return "cancelled";
        }
        else
        {
            List<KMSelectable> pegs = new List<KMSelectable>();
            bool skipped = false;
            foreach (string pegtopress in split)
            {
                switch (GetPosition(pegtopress))
                {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                    case 4:
                        KMSelectable peg = Pegs[GetPosition(pegtopress)];
                        if (pegs.Contains(peg))
                        {
                            yield return string.Format("sendtochaterror I can't press the {0} peg more than once.", new[] {"top", "top right", "bottom right", "bottom left", "top left"}[GetPosition(pegtopress)]);
                            yield break;
                        }
                        pegs.Add(peg);
                        break;
                    case -3:    //Press/submit
                        skipped = true;
                        break;
                    default:
                        yield return !pegs.Any() && !skipped 
                            ? string.Format("sendtochaterror Valid commands are 'look', 'rotate', 'press', or 'submit'") 
                            : string.Format("sendtochaterror I don't know what you mean by the {0} peg.", pegtopress);
                        yield break;
                }
                
            }

            if (pegs.Count != 3)
            {
                yield return string.Format("sendtochaterror I need exactly which three pegs to press. You told me to press {0} peg{1}.", pegs.Count, pegs.Count != 1 ? "s" : "");
                yield break;
            }

            yield return null;
            foreach (KMSelectable peg in pegs)
            {
                peg.OnInteract();
                yield return new WaitForSeconds(DURATION);
            }
            yield return new WaitForSeconds(DURATION);
        }
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
