#region "VERSION HISTORY"
// Build Version: 33 (Tuesday, April 21st, 2009)
// (See 'Engine Changes.txt' for specific updates.)
#endregion

/*
// Future:
//   * When parsing the script, parse everything else as well, if statements,
//     loop statements, bookmarks, etc... to make everything easier to locate
//     in run-time.
//
// Notes:
//   * File support via scripting is disabled until future improvement.
*/

#region "Helper Code Examples"
// Example Helper Code:
/*public DSSL_External_DoCommand(ByVal ModuleName as string, ByVal ProcedureName as string, ByVal ArgumentsCount As Long, ArgumentsItem() as string, ByRef outRetCount As Long) as string()
'    
'
'    Dim strRet() as string
'    outRetCount = 0
'    Erase strRet
'    DSSL_External_DoCommand = strRet
'
'    switch(LCase$(ModuleName)
'    case("test"
'        switch(LCase$(ProcedureName)
'        case("msgbox"
'            if(ArgumentsCount >= 1) {
'                MsgBox(ArgumentsItem(1))
'            }
'        case("inputbox"
'            if(ArgumentsCount >= 1) {
'                outRetCount = 1
'                ReDim strRet(outRetCount)
'                strRet(1) = InputBox(ArgumentsItem(1))
'            }
'        }
'    }
'
'    DSSL_External_DoCommand = strRet
'} //
'public DSSL_External_DoOperator(ByVal Operator as string, ByVal leftOf as string, ByVal rightOf as string, ByRef AnyFound As bool) as string
'    
'
'    Dim strRet as string
'    strRet = ""
'    AnyFound = false
'    DSSL_External_DoOperator = strRet
'
'    switch(LCase$(Operator)
'    // Case "test"
'    //    switch(LCase$(ProcedureName)
'    //    case("msgbox"
'    //        if(ArgumentsCount >= 1) {
'    //            MsgBox(ArgumentsItem(1))
'    //        }
'    //    case("inputbox"
'    //        if(ArgumentsCount >= 1) {
'    //            strRet = InputBox(ArgumentsItem(1))
'    //        }
'    //    }
'    }
'
'    DSSL_External_DoOperator = strRet
'} //
'public DSSL_External_DoExpressionFunction(ByVal functionName as string, ByVal ArgumentsCount As Long, ArgumentsItem() as string, ByRef AnyFound As bool) as string
'    
'
'    Dim strRet as string
'    strRet = ""
'    AnyFound = false
'    DSSL_External_DoExpressionFunction = strRet
'
'    switch(LCase$(functionName)
'    case("rgb"
'        if(ArgumentsCount == 3) {
'            AnyFound = true;
'            strRet = RGB(System.Convert.ToInt32(ArgumentsItem(0)), System.Convert.ToInt32(ArgumentsItem(1)), System.Convert.ToInt32(ArgumentsItem(2)))
'        }
'    }
'
'    return strRet;
'} //*/
#endregion

using System.Collections.Generic;

namespace DSSL
{
    /// <summary>Contains global settings for the scripting engine.</summary>
    public static class Settings
    {
        public static bool   DontAllowExternalFunctions   = false;
        public static bool   DontAllowExternalExpressions = false;
        public static bool   DontAllowFileOperations      = false;
        public static string LibraryPath                  = "";
        public static bool   LinkVariables                = false;
        public static bool   LogErrors                    = false;
        public static bool   TreatUnknownLinesAsComments  = false;
    } //Settings

    public static class Globals
    {
        public  static Runtime.Environment   MyEnvironment = new Runtime.Environment();  //Create a global of the environment instance.
        public  static Runtime.Instances     MyInstances   = new Runtime.Instances();      //Create a instance to the instances class.
        public  static BaseHelper    MyHelper      = null;
        private static ErrorsManager mErrors       = new ErrorsManager();

        public static ErrorsManager Errors { get { return mErrors; } }
    } // Globals

    public interface BaseHelper
    {
        List<string> DoCommand(string ModuleName, string ProcedureName, List<string> Arguments);
        string DoOperator(string Operator, string leftOf, string rightOf, out bool AnyFound);
        string DoExpressionFunction(string functionName, List<string> Arguments, out bool AnyFound);
    } //BaseHelper

    ///<summary>Contains logic to help parse a script file.</summary>
    public static class Parser
    {
        private const string ExpressionOperators = "!=,<=,>=,mod,pwr,+,-,*,/,\\,=,<,>,%";

#region "Enumerations"
        public enum OptionFlags { LogErrors, TreatUnknownLinesAsComments }

        public enum Comparisons { Equals, Less, More, EqualsOrMore, EqualsOrLess, NotEquals }

        public enum FunctionAccess { Private, Public, Global }

        public enum VariableScopes { Global, Public, Private }

        public enum FunctionTypes { Header, End, Exit, Results, Call }

        public enum VariableTypes { Remove, Set, Get, Add }

        public enum LoopTypes { Start, End, Stop }

        public enum IfStatementTypes { If, ElseIf, Else, EndIf }

        public enum BookmarkTypes { Set, GoTo }

        public enum DecisionTypes { Start, Item, Default, End }
#endregion //Enumerations

        public static class Translator
        {
            // Determine what comparison test is in the string, but only search for one.
            public static Comparisons ComparisonToValue(string value, out int outPos, out int outSize)
            {
                // Set defaults
                outPos  = 0;
                outSize = 0;

                // Find each type of comparison operator.
                int foundPos = StringManager.FindStringOutside(value, 1, "!=");
                if(foundPos != 0) { outPos = foundPos; outSize = 2; return Comparisons.NotEquals; }
                foundPos = StringManager.FindStringOutside(value, 1, ">=");
                if(foundPos != 0) { outPos = foundPos; outSize = 2; return Comparisons.EqualsOrMore; }
                foundPos = StringManager.FindStringOutside(value, 1, "<=");
                if(foundPos != 0) { outPos = foundPos; outSize = 2; return Comparisons.EqualsOrLess; }
                foundPos = StringManager.FindStringOutside(value, 1, "=");
                if(foundPos != 0) { outPos = foundPos; outSize = 1; return Comparisons.Equals; }
                foundPos = StringManager.FindStringOutside(value, 1, "<");
                if(foundPos != 0) { outPos = foundPos; outSize = 1; return Comparisons.Less; }
                foundPos = StringManager.FindStringOutside(value, 1, ">");
                if(foundPos != 0) { outPos = foundPos; outSize = 1; return Comparisons.More; }

                // By default return equals.
                return Comparisons.Equals;
            } //ComparisonToValue

            public static string ComparisonToString(Comparisons comparisonType)
            {
                switch(comparisonType) {
                    case(Parser.Comparisons.NotEquals):    return "!=";
                    case(Parser.Comparisons.EqualsOrMore): return ">=";
                    case(Parser.Comparisons.EqualsOrLess): return "<=";
                    case(Parser.Comparisons.Less):         return "<";
                    case(Parser.Comparisons.More):         return ">";
                    default:                               return "=";
                }
            } //ComparisonToString

            public static string VariableScopeToString(VariableScopes value)
            {
                switch(value) {
                    case(VariableScopes.Private): return "Private";
                    case(VariableScopes.Public):  return "Public";
                    default:                      return "Global";
                }
            } //VariableScopeToString

            public static VariableScopes VariableScopeToEnum(string value)
            {
                switch(value.ToLower()) {
                    case("global"):  return VariableScopes.Global;
                    case("public"):  return VariableScopes.Public;
                    case("private"):
                    case("local"):
                    case("locale"):  return VariableScopes.Private;
                    default:         return VariableScopes.Private;
                }
            } //VariableScopeToEnum

            public static string FunctionAccessToString(FunctionAccess value)
            {
                switch(value) {
                    case(FunctionAccess.Public):  return "Public";
                    case(FunctionAccess.Private): return "Private";
                    default:                      return "Global";
                }
            } //FunctionAccessToString

            public static FunctionAccess FunctionAccessToEnum(string value)
            {
                switch (value.ToLower())
                {
                    case ("public"): return FunctionAccess.Public;
                    case ("private"): return FunctionAccess.Private;
                    default: return FunctionAccess.Global;
                }
            } //FunctionAccessToString

            public static string FunctionTypeToString(FunctionTypes value)
            {
                switch(value) {
                    case (FunctionTypes.Header): return "Header";
                    case (FunctionTypes.End): return "End";
                    case (FunctionTypes.Exit): return "Exit";
                    case (FunctionTypes.Results): return "Results";
                    case (FunctionTypes.Call): return "Call";
                    default: return "Call";
                }
            }

            public static FunctionTypes FunctionTypeToEnum(string value)
            {
                switch(value.ToLower()) {
                    case ("0"): case ("header"): return FunctionTypes.Header;
                    case ("1"): case ("end"): return FunctionTypes.End;
                    case ("2"): case ("exit"): return FunctionTypes.Exit;
                    case ("3"): case ("results"): return FunctionTypes.Results;
                    case ("4"): case ("call"): return FunctionTypes.Call;
                    default: return FunctionTypes.Call;
                }
            }
        } //Translator

        public static class Syntax
        {
            private const string VariableOperatorValue = "&&";
            private const string VariableOperatorLink  = "&$";
            private const string BracketStart          = "[";
            private const string BracketEnd            = "]";
            private const string ArgumentSeparator     = ",";
            private const string PropertySeparator     = ".";
            private const string Space                 = " ";
            private const string OperatorCharacter     = "&";

            /// <param name="inNameTag">Must be in the format: name[index]</param>
            public static bool ParseArrayNameData(string inNameTag, out string outName, out string outIndex)
            {
                // Set defaults.
                outName  = "";
                outIndex = "0";

                int leftBracketPos = inNameTag.IndexOf(BracketStart, 0);
                if(leftBracketPos < 0)
                    outName = inNameTag;
                else {
                    int rightBracketPos = inNameTag.IndexOf(BracketEnd, leftBracketPos);
                    if(rightBracketPos < 0) return false;

                    outName = inNameTag.Substring(0, leftBracketPos - 1);
                    outIndex = inNameTag.Substring(leftBracketPos, rightBracketPos - (leftBracketPos + 1));
                }

                return true;
            } //ParseArrayNameData

            /// <param name="inValueTag">Must be in the format: [0, 5, ...]</param>
            public static List<string> ParseArrayValueData(string inValueTag)
            {
                List<string> results = new List<string>();

                int bracketStartPos = inValueTag.IndexOf(BracketStart, 0);
                if(bracketStartPos < 0)
                    results.Add(inValueTag);
                else {
                    int bracketEndPos = inValueTag.IndexOf(BracketEnd, bracketStartPos);
                    if(bracketEndPos < 0) return null;

                    string tagBody = inValueTag.Substring(bracketStartPos, bracketEndPos - (bracketStartPos + 1));
                    StringManager.SmartSplit(tagBody, ArgumentSeparator, out results);
                }

                return results;
            } //ParseArrayValueData

            public static void ParseFunctionCallName(string inString, out string outParentName, out string outFunctionName)
            {
                outParentName   = "";
                outFunctionName = "";

                int seperatorPos = StringManager.FindStringOutside(inString, 1, PropertySeparator, 0, "", true);
                if(seperatorPos == 0)
                    outFunctionName = inString;
                else {
                    outParentName = inString.Substring(0, seperatorPos - 1);
                    outFunctionName = inString.Substring(seperatorPos);
                }
            } //ParseFunctionCallName

            public static bool IsStandardCharacter(char character, bool includeLowerCaseLetters = true, bool includeUpperCaseLetters = true, bool includeNumbers = false, bool includeSpecialCharacter = false, bool includeArrayData = false, bool includeAddressCharacter = false)
            {
                // Get the charcter's ASCII index.
                int charCode = System.Convert.ToInt32(character);
                int charLowerA = System.Convert.ToInt32('a');
                int charUpperA = System.Convert.ToInt32('A');
                int charLowerZ = System.Convert.ToInt32('z');
                int charUpperZ = System.Convert.ToInt32('Z');
                int charLowerNumber = System.Convert.ToInt32('0');
                int charUpperNumber = System.Convert.ToInt32('9');
                int charLowerBracket = System.Convert.ToInt32('[');
                int charUpperBracket = System.Convert.ToInt32(']');
                int charSpecial = System.Convert.ToInt32('_');
                int charAddress = System.Convert.ToInt32('#');
                
                // if(we should check for lower-case letters then ...
                if(includeLowerCaseLetters && charCode >= charLowerA && charCode <= charLowerZ) return true;
                // if(we should check for upper-case letters then ...
                if(includeUpperCaseLetters && charCode >= charUpperA && charCode <= charUpperZ) return true;
                // if(we should check for numbers then ...
                if(includeNumbers && charCode >= charLowerNumber && charCode <= charUpperNumber) return true;
                // ... if(this is a lower-case letter then ...
                if(includeArrayData && (charCode == charLowerBracket || charCode == charUpperBracket)) return true;
                // ... if(this is a lower-case letter then ...
                if(includeSpecialCharacter && charCode == charSpecial) return true;
                // ... if(this is a lower-case letter then ...
                if(includeAddressCharacter && charCode == charAddress) return true;

                return false;
            } //IsStandardCharacter

            public static bool IsOperatorName(string name)
            {
                // Split the operators up to look through it.
                List<string> operators; StringManager.SmartSplit(ExpressionOperators, ArgumentSeparator, out operators);

                // Go through each operator ...
                foreach(string op in operators) {
                    // ... if(the operator name matches then ... return successful.
                    if(string.Equals(op, name, System.StringComparison.CurrentCultureIgnoreCase)) return true;
                } // dwOperator

                return false;
            } //IsOperatorName

            // Rules:
            //   * Only a few characters can be attached to the end of a operator and
            //     still treated like a variable. These characters is a space // // and
            //     the expression character '&'.
            public static int FindEndOfOperator(string str, int start = 1)
            {
                int[] pos = new int[2];
                pos[0] = StringManager.FindStringOutside(str, start, Space, 0, "", true);
                pos[1] = StringManager.FindStringOutside(str, start, OperatorCharacter, 0, "", true);

                if(pos[0] != 0) {
                    if(pos[1] != 0) {
                        return ((pos[1] < pos[0]) ? pos[1] : pos[0]);
                    } else {
                        return pos[0];
                    }
                } else if(pos[1] != 0)
                    return pos[1];

                return 0;
            } //FindEndOfOperator

            public static int FindEndOfSyntax(string expression, int startPosition, bool includeLowerCaseLetters = true, bool includeUpperCaseLetters = true, bool includeNumbers = false, bool includeSpecialCharacter = false, bool includeArrayData = false, bool includeAddressCharacter = false)
            {
                char lineChar;
                for(int position = startPosition; position <= expression.Length; position++) {
                    lineChar = expression[position - 1]; //.Substring(position - 1, 1);

                    if(!IsStandardCharacter(lineChar, includeLowerCaseLetters, includeUpperCaseLetters, includeNumbers, includeSpecialCharacter, includeArrayData, includeAddressCharacter)) {
                        return position;
                    }
                } //position

                // return defaults.
                return (expression.Length + 1);
            } //FindEndOfSyntax

            public static bool IsLinkTag(string tag, out string outName)
            {
                outName = "";

                if(tag.StartsWith(VariableOperatorLink, System.StringComparison.CurrentCultureIgnoreCase)) {
                    outName = tag.Substring(VariableOperatorLink.Length, tag.Length - VariableOperatorLink.Length);
                    return true;
                } else {
                    outName = "";
                    return false;
                }
            } //IsLinkTag

            // Removes special variable characters from the name.
            public static string FixVariableName(string name)
            {
                name = name.Trim();

                // Fix the variable's name.
                if(name.StartsWith(VariableOperatorValue, System.StringComparison.CurrentCultureIgnoreCase))
                    return name.Substring(VariableOperatorValue.Length, name.Length - VariableOperatorValue.Length);
                else if(name.StartsWith(VariableOperatorLink, System.StringComparison.CurrentCultureIgnoreCase))
                    return name.Substring(VariableOperatorLink.Length, name.Length - VariableOperatorLink.Length);
                else
                    return name;
            } //FixVariableName
        } //Syntax
    } //Parser

    public static class Framework
    {
#region "Enumerations"
        public enum EventCommands
        {
            Nothing,
            Comment,
            Function,
            IfStatement,
            LoopStatement,
            External,
            ScriptID,
            DoExpression,
            VariableCommand,
            BookmarkCommand,
            Stop,
            DecisionCondition,
            Include,
            Count //does nothing
        } //EventCommands

        public enum EventParameterTypes { Unknown, Value, Function }

        public enum FileAppendModes { Read, Save }
#endregion //Enumerations

#region "Constants"
        private const string ScriptBinaryHeader         = "SSDSSLSCRIPT";
        private const int    ScriptBinaryVersionMinimum = 1;
        private const int    ScriptBinaryVersionCurrent = 1;
        private const string ScriptEventsBinaryHeader   = "EVENTS";
        private const int    ScriptEventsVersionMinimum = 1;
        private const int    ScriptEventsVersionCurrent = 1;
#endregion //Constants

#region "Managers"
        public class ScriptsCollection : List<Script> {}

        public class Script
        {
            public string           FileName = "";
            public EventsCollection Events   = new EventsCollection();
            public EventsCollection Pointers = new EventsCollection();

            public Script() {}

            public Script(Script clone)
            {
                if(clone == null) return;
                this.FileName = clone.FileName;
                this.Events   = new EventsCollection(clone.Events);
                this.Pointers = new EventsCollection(clone.Pointers);
            } //Constructor

            public static bool IsFileValid(string fileName)
            {
                if(!System.IO.File.Exists(fileName)) return false;
                System.IO.FileStream   ioFile  = System.IO.File.Open(fileName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                System.IO.BinaryReader bufFile = new System.IO.BinaryReader(ioFile);

                string strHeader = "";
                int    dwVersion = 0;
                FileManager.File_ReadHeader(bufFile, ScriptBinaryHeader.Length, out strHeader, out dwVersion);

                bool headerGood  = strHeader.Equals(ScriptBinaryHeader);
                bool versionGood = (dwVersion >= ScriptBinaryVersionMinimum && dwVersion <= ScriptBinaryVersionCurrent);

                bufFile.Close(); //does ioFile.Close() also

                return (headerGood && versionGood);
            } //IsFileValid

            public bool LoadFromFile(string fileName)
            {
                System.IO.FileStream   ioFile  = null;
                System.IO.BinaryReader bufFile = null;
                try {
                    if(!System.IO.File.Exists(fileName)) return false;
                    ioFile = System.IO.File.Open(fileName, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                    this.FileName = fileName;
                    bufFile = new System.IO.BinaryReader(ioFile);
                } catch { //(System.Exception ex) {
                    if(ioFile != null) ioFile.Close();
                    return false;
                }
                if(ioFile == null) return false;

                string strHeader = "";
                int    dwVersion = 0;
                FileManager.File_ReadHeader(bufFile, ScriptBinaryHeader.Length, out strHeader, out dwVersion);
                if(!string.Equals(strHeader, ScriptBinaryHeader)) { bufFile.Close(); return false; }
                if(dwVersion < ScriptBinaryVersionMinimum || dwVersion > ScriptBinaryVersionCurrent) { bufFile.Close(); return false; }

                if(!this.Events.LoadFromStream(bufFile))   { bufFile.Close(); return false; }
                if(!this.Pointers.LoadFromStream(bufFile)) { bufFile.Close(); return false; }

                bufFile.Close();
                return true;
            } //LoadFromFile

            public bool SaveToFile(string fileName)
            {
                System.IO.FileStream   ioFile = null;
                System.IO.BinaryWriter bufFile;
                try {
                    ioFile  = System.IO.File.Open(fileName, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
                    bufFile = new System.IO.BinaryWriter(ioFile);
                } catch {
                    if(ioFile != null) ioFile.Close();
                    return false;
                }
                if(ioFile == null) return false;

                string strHeader = ScriptBinaryHeader;
                int    dwVersion = ScriptBinaryVersionCurrent;
                FileManager.File_WriteHeader(bufFile, strHeader, dwVersion);

                this.Events.SaveToStream(bufFile);
                this.Pointers.SaveToStream(bufFile);

                bufFile.Close();
                return true;
            } //SaveToFile
        } //Script

        public class EventsCollection : List<Event>
        {
            public EventsCollection() {}

            public EventsCollection(EventsCollection clone)
            {
                if(clone == null || clone.Count == 0) return;
                for(int index = 0; index < clone.Count; index++) {
                    base.Add(new Event(clone[index]));
                } //index
            } //Constructor

            public Event Add(EventCommands command, bool disabled)
            {
                base.Add(new Event(command, disabled));
                return base[base.Count - 1];
            } //Add

            public Event Add(string pointerID, EventCommands command, bool disabled)
            {
                base.Add(new Event(pointerID, command, disabled));
                return base[base.Count - 1];
            } //Add

            public int Add(EventsCollection events)
            {
                if(events == null || events.Count == 0) return -1;

                int firstIndex = this.Count;

                for(int index = 0; index < events.Count; index++) {
                    this.Add(events[index]);
                } //index

                return firstIndex;
            } //Add

            /// <returns>Returns an index for the pointer (1-based.)</returns>
            public int FindByPointerID(string pointerID)
            {
                if(base.Count != 0) {
                    for(int index = 0; index < base.Count; index++) {
                        if(string.Equals(base[index].PointerID, pointerID, System.StringComparison.CurrentCultureIgnoreCase)) return (1 + index);
                    } //index
                }

                return 0;
            } //FindByPointerID

            public bool LoadFromStream(System.IO.BinaryReader fileData)
            {
                string strHeader = "";
                int    dwVersion = 0;
                FileManager.File_ReadHeader(fileData, ScriptEventsBinaryHeader.Length, out strHeader, out dwVersion);
                if(!string.Equals(strHeader, ScriptEventsBinaryHeader)) return false;
                if(dwVersion < ScriptEventsVersionMinimum || dwVersion > ScriptEventsVersionCurrent) return false;

                int newCount = fileData.ReadInt32();

                if(newCount != 0) {
                    for(int index = 0; index < newCount; index++) {
                        Event newItem = new Event();
                        newItem.LoadFromStream(fileData);
                        base.Add(newItem);
                    } //index
                }

                return true;
            } //LoadFromStream

            public void SaveToStream(System.IO.BinaryWriter fileData)
            {
                string strHeader = ScriptEventsBinaryHeader;
                int    dwVersion = ScriptEventsVersionCurrent;
                FileManager.File_WriteHeader(fileData, strHeader, dwVersion);

                fileData.Write(this.Count);

                if(base.Count > 0) {
                    for(int index = 0; index < base.Count; index++) {
                        base[index].SaveToStream(fileData);
                    } //index
                }
            } //SaveToStream
        } //EventsCollection

        public class Event
        {
            public EventCommands Command = EventCommands.Nothing;
            public string        PointerID = "";
            public bool          Disabled = false;
            public ParametersCollection Parameters = new ParametersCollection();

            public Event() {}

            public Event(EventCommands command, bool disabled)
            {
                this.PointerID = "";
                this.Command   = command;
                this.Disabled  = disabled;
            } //Constructor

            public Event(string pointerID, EventCommands command, bool disabled)
            {
                this.PointerID = pointerID;
                this.Command   = command;
                this.Disabled  = disabled;
            } //Constructor

            public Event(Event clone)
            {
                if(clone == null) return;
                this.Command = clone.Command;
                this.PointerID = clone.PointerID;
                this.Disabled = clone.Disabled;
                this.Parameters = new ParametersCollection(clone.Parameters);
            } //Constructor

            public bool LoadFromStream(System.IO.BinaryReader fileData)
            {
                this.PointerID = FileManager.File_ReadString(fileData);
                this.Command = (EventCommands)fileData.ReadInt32();
                this.Disabled = fileData.ReadBoolean();
                this.Parameters.LoadFromStream(fileData);

                return true;
            } //LoadFromStream

            public void SaveToStream(System.IO.BinaryWriter fileData)
            {
                FileManager.File_WriteString(fileData, this.PointerID);
                fileData.Write(System.Convert.ToInt32(this.Command));
                fileData.Write(this.Disabled);
                this.Parameters.SaveToStream(fileData);
            } //SaveToStream
        } //Event

        public class ParametersCollection : List<Parameter>
        {
            public ParametersCollection() {}

            public ParametersCollection(ParametersCollection clone)
            {
                if(clone == null || clone.Count == 0) return;
                for(int index = 0; index < clone.Count; index++) {
                    base.Add(new Parameter(clone[index]));
                } //index
            } //Constructor

            public Parameter AddFunction(string functionID)
            {
                base.Add(Parameter.AsFunction(functionID));
                return base[base.Count - 1];
            } //AddFunction

            public Parameter AddValue(string value)
            {
                base.Add(Parameter.AsValue(value));
                return base[base.Count - 1];
            } //AddValue

            public Parameter GetItem(int index, string def = "")
            {
                return ((index > base.Count) ? Parameter.AsValue(def) : base[index - 1]);
            } //GetItem

            public string GetValue(int index, string def = "")
            {
                if(index > base.Count)
                    return def;
                else
                    return ((base[index - 1].Type == EventParameterTypes.Value) ? base[index - 1].Value : def);
            } //GetValue

            public bool LoadFromStream(System.IO.BinaryReader fileData)
            {
                int newCount = fileData.ReadInt32();

                if(newCount != 0) {
                    for(int index = 0; index < newCount; index++) {
                        Parameter newItem = new Parameter();
                        newItem.LoadFromStream(fileData);
                        base.Add(newItem);
                    } //index
                }

                return true;
            } //LoadFromStream

            public void SaveToStream(System.IO.BinaryWriter fileData)
            {
                fileData.Write(base.Count);

                if(base.Count > 0) {
                    for(int index = 0; index < base.Count; index++) {
                        base[index].SaveToStream(fileData);
                    } //index
                }
            } //SaveToStream
        } //ParametersCollection

        public class Parameter
        {
            public EventParameterTypes Type = EventParameterTypes.Unknown;
            public string Value = "";

            public Parameter() {}

            public Parameter(Parameter clone)
            {
                if(clone == null) return;
                this.Type = clone.Type;
                this.Value = clone.Value;
            } //Constructor

            public static Parameter AsFunction(string functionID)
            {
                Parameter result = new Parameter();
                result.Type = EventParameterTypes.Function;
                result.Value = functionID;
                return result;
            } //AsFunction

            public static Parameter AsValue(string value)
            {
                Parameter result = new Parameter();
                result.Type = EventParameterTypes.Value;
                result.Value = value;
                return result;
            } //AsValue

            public void LoadFromStream(System.IO.BinaryReader fileData)
            {
                this.Type = (EventParameterTypes)fileData.ReadInt32();
                string valueData = FileManager.File_ReadString(fileData);
                string pointerID = FileManager.File_ReadString(fileData);
                if(this.Type == EventParameterTypes.Value)
                    this.Value = valueData;
                else if(this.Type == EventParameterTypes.Function)
                    this.Value = pointerID;
            } //LoadFromStream

            public void SaveToStream(System.IO.BinaryWriter fileData)
            {
                fileData.Write(System.Convert.ToInt32(this.Type));
                string valueData = "";
                string pointerID = "";
                if(this.Type == EventParameterTypes.Value)
                    valueData = this.Value;
                else if(this.Type == EventParameterTypes.Function)
                    pointerID = this.Value;
                FileManager.File_WriteString(fileData, valueData);
                FileManager.File_WriteString(fileData, pointerID);
            } //SaveToStream
        } //Parameter
#endregion //Managers

        public static class Properties
        {
            public class IncludeStatement
            {
                public Parameter FileName;

                public bool Parse(Event eventLine)
                {
                    if(eventLine.Command != Framework.EventCommands.Include) return false;
                    this.FileName = eventLine.Parameters.GetItem(1, "");
                    return true;
                } //Parse
            } //IncludeStatement

            public class DecisionStatement
            {
                public Parser.DecisionTypes Type;
                public Parameter            Value;
                public Parser.Comparisons   Comparison;

                public bool Parse(Event eventLine)
                {
                    if(eventLine.Command != Framework.EventCommands.DecisionCondition) return false;
                    this.Type = (Parser.DecisionTypes)System.Convert.ToInt32(eventLine.Parameters.GetValue(1, ""));
                    this.Value = eventLine.Parameters.GetItem(2, "");
                    this.Comparison = (Parser.Comparisons)System.Convert.ToInt32(eventLine.Parameters.GetValue(3, "0"));
                    return true;
                } //Parse
            } //DecisionStatement

            public class BookmarkStatement
            {
                public Parser.BookmarkTypes Type;
                public Parameter            Name;

                public bool Parse(Event eventLine)
                {
                    if(eventLine.Command != Framework.EventCommands.BookmarkCommand) return false;
                    this.Type = (Parser.BookmarkTypes)System.Convert.ToInt32(eventLine.Parameters.GetValue(1, "0"));
                    this.Name = eventLine.Parameters.GetItem(2, "");
                    return true;
                } //Parse
            } //BookmarkStatement

            public class DoExpressionStatement
            {
                public string Expression = "";

                public bool Parse(Event eventLine)
                {
                    if(eventLine.Command != Framework.EventCommands.DoExpression) return false;
                    this.Expression = eventLine.Parameters.GetValue(1, "");
                    return true;
                } //Parse
            } //DoExpressionStatement

            public class CommentStatement
            {
                public string Text = "";

                public bool Parse(Event eventLine)
                {
                    if(eventLine.Command != Framework.EventCommands.Comment) return false;
                    this.Text = eventLine.Parameters.GetValue(1, "");
                    return true;
                } //Parse
            } //CommentStatement

            public class FunctionStatement
            {
                public Parser.FunctionTypes  Type;
                public Parser.FunctionAccess Access;
                public string                Name       = "";
                public List<string>          Results    = new List<string>();
                public List<Parameter>       Properties = new List<Parameter>();

                public bool Parse(Event eventLine)
                {
                    if(eventLine.Command != Framework.EventCommands.Function) return false;

                    this.Type = Parser.Translator.FunctionTypeToEnum(eventLine.Parameters.GetValue(1, "Header"));
                    this.Access = Parser.FunctionAccess.Global;
                    this.Name = "";
                    this.Results.Clear();
                    this.Properties.Clear();

                    if(this.Type == Parser.FunctionTypes.Results) {
                        for(int index = 0; index < this.Results.Count; index++) {
                            this.Results.Add(eventLine.Parameters.GetValue(2 + index, ""));
                        } // dwProp
                    } else {
                        this.Name = eventLine.Parameters.GetValue(2, "");
                        this.Access = Parser.Translator.FunctionAccessToEnum(eventLine.Parameters.GetValue(3, "0"));
                        if(eventLine.Parameters.Count > 3) {
                            for(int index = 0; index < this.Properties.Count; index++) {
                                this.Properties.Add(eventLine.Parameters.GetItem(4 + index, ""));
                            } // dwProp
                        }
                    }

                    return true;
                } //Parse
            } //FunctionStatement

            public class IfStatement
            {
                public Parser.IfStatementTypes Type;
                public Parser.Comparisons Compare;
                public Parameter Value1;
                public Parameter Value2;

                public bool Parse(Event eventLine)
                {
                    if(eventLine.Command != Framework.EventCommands.IfStatement) return false;
                    this.Type    = (Parser.IfStatementTypes)System.Convert.ToInt32(eventLine.Parameters.GetValue(1, ""));
                    this.Compare = (Parser.Comparisons)System.Convert.ToInt32(eventLine.Parameters.GetValue(2, "0"));
                    this.Value1  = eventLine.Parameters.GetItem(3, "");
                    this.Value2  = eventLine.Parameters.GetItem(4, "");
                    return true;
                } //Parse

                public Event ToEvent()
                {
                    Event result = new Event();
                    result.Disabled = false;
                    result.Command = Framework.EventCommands.IfStatement;
                    result.PointerID = "";
                    result.Parameters.AddValue(this.Type.ToString());
                    result.Parameters.AddValue(this.Compare.ToString());
                    result.Parameters.Add(this.Value1);
                    result.Parameters.Add(this.Value2);
                    return result;
                } //ToEvent
            } //IfStatement

            public class LoopStatement
            {
                public Parser.LoopTypes Type;

                public bool Parse(Event eventLine)
                {
                    if(eventLine.Command != Framework.EventCommands.LoopStatement) return false;
                    this.Type = (Parser.LoopTypes)System.Convert.ToInt32(eventLine.Parameters.GetValue(1, ""));
                    return true;
                } //Parse

                public Event ToEvent()
                {
                    Event result = new Event();
                    result.Disabled = false;
                    result.Command = Framework.EventCommands.LoopStatement;
                    result.PointerID = "";
                    result.Parameters.AddValue(this.Type.ToString());
                    return result;
                } //ToEvent
            } //LoopStatement

            public class VariableCommandNameStatement
            {
                public string Name = "";
                public string Index = "";

                public VariableCommandNameStatement() {}

                public VariableCommandNameStatement(string name, string index)
                {
                    this.Name = name;
                    this.Index = index;
                } //Constructor
            } //VariableCommandNameStatement

            public class VariableCommandNamesStatement : List<VariableCommandNameStatement>
            {
                public override string ToString()
                {
                    string results = "";

                    if(base.Count != 0) {
                        string strName;
                        for(int index = 0; index < base.Count; index++) {
                            strName = base[index].Name;
                            if(System.Convert.ToInt32(base[index].Index) != 0) {
                                strName += "[" + base[index].Index + "]";
                            }

                            results = (string.IsNullOrEmpty(results) ? "" : results +  ", ") + strName;
                        } // dwIndex
                    }

                    return results;
                } //
            } //VariableCommandNamesStatement

            public class VariableCommandValuesStatement : List<Parameter> {}

            public class VariableCommandStatement
            {
                public Parser.VariableTypes   Type;
                public VariableCommandNamesStatement  Names  = new VariableCommandNamesStatement();
                public VariableCommandValuesStatement Values = new VariableCommandValuesStatement();
                public Parser.VariableScopes  Scope;
                public string Default;

                //SET
                //  (2) NAMES_COUNT
                //  (3) NAMES_ITEM
                //  (... + 1) VALUES_COUNT
                //  (... + 1 ...) VALUES_ITEM
                //DELETE
                //  (2) NAMES_COUNT
                //  (3) NAMES_ITEM
                //GET
                //  (2) NAME
                //  (3) VALUE
                //ADD
                //  (2) NAME
                //  (3) VALUE
                //  (4) SCOPE
                public bool Parse(Event eventLine)
                {
                    if(eventLine.Command != Framework.EventCommands.VariableCommand) return false;

                    this.Type = (Parser.VariableTypes)System.Convert.ToInt32(eventLine.Parameters.GetValue(1, "0"));

                    if(this.Type == Parser.VariableTypes.Set) {
                        // Get the names count
                        int namesCount = System.Convert.ToInt32(eventLine.Parameters.GetValue(2, ""));

                        if(namesCount != 0) {
                            string strName;
                            for(int index = 0; index < namesCount; index++) {
                                strName = eventLine.Parameters.GetValue(3 + index, "");
                                Parser.Syntax.ParseArrayNameData(strName, out this.Names[index].Name, out this.Names[index].Index);
                            } // index
                        }

                        int valuesCount = System.Convert.ToInt32(eventLine.Parameters.GetValue(2 + namesCount + 1, ""));

                        if(valuesCount != 0) {
                            for(int index = 0; index < valuesCount; index++) {
                                this.Values[index] = eventLine.Parameters.GetItem(2 + namesCount + 2 + index, "");
                            } // index
                        }
                    } else if(this.Type == Parser.VariableTypes.Remove) {
                        //RetDesc.name = Framework.EventParameters_GetValue(eventLine.Parameters, 2, "");

                        // Get the names count
                        int namesCount = System.Convert.ToInt32(eventLine.Parameters.GetValue(2, ""));

                        if(namesCount != 0) {
                            string strName;
                            for(int index = 0; index < namesCount; index++) {
                                strName = eventLine.Parameters.GetValue(3 + index, "");
                                Parser.Syntax.ParseArrayNameData(strName, out this.Names[index].Name, out this.Names[index].Index);
                            } // index
                        }
                    } else if(this.Type == Parser.VariableTypes.Get) {
                        string strName = eventLine.Parameters.GetValue(2, "");
                        string parsedName, parsedIndex;
                        Parser.Syntax.ParseArrayNameData(strName, out parsedName, out parsedIndex);
                        this.Names.Add(new VariableCommandNameStatement(parsedName, parsedIndex));
                        //
                        this.Values.Add(eventLine.Parameters.GetItem(3, ""));
                    } else if(this.Type == Parser.VariableTypes.Add) {
                        string strName = eventLine.Parameters.GetValue(2, "");
                        string parsedName, parsedIndex;
                        Parser.Syntax.ParseArrayNameData(strName, out parsedName, out parsedIndex);
                        this.Names.Add(new VariableCommandNameStatement(parsedName, parsedIndex));
                        //
                        this.Values.Add(eventLine.Parameters.GetItem(3, ""));
                        this.Scope = (Parser.VariableScopes)System.Convert.ToInt32(eventLine.Parameters.GetValue(4, Parser.VariableScopes.Private.ToString()));
                    }

                    return true;
                } //Parse
            } //VariableCommandStatement

            public class ScriptIDStatement
            {
                public string Name = "";

                public ScriptIDStatement() {}

                public ScriptIDStatement(string name) { this.Name = name; }
            } //ScriptIDStatement

            public class ExternalArgumentStatement
            {
                public Parameter Value;

                public ExternalArgumentStatement() {}

                public ExternalArgumentStatement(Parameter value) { this.Value = value; }
            } //ExternalArgumentStatement

            public class ExternalArgumentsStatement : List<ExternalArgumentStatement> {}

            public class ExternalStatement
            {
                public string ModuleName = "";
                public string ProcName   = "";
                public ExternalArgumentsStatement Arguments = new ExternalArgumentsStatement();

                public bool Parse(Event eventLine)
                {
                    if(eventLine.Command != Framework.EventCommands.External) return false;

                    this.ModuleName = eventLine.Parameters.GetValue(1, "");
                    this.ProcName = eventLine.Parameters.GetValue(2, "");
                    this.Arguments.Clear();

                    if(eventLine.Parameters.Count > 2) {
                        int argumentsCount = (eventLine.Parameters.Count - 2);
                        for(int index = 0; index < argumentsCount; index++) {
                            this.Arguments.Add(new ExternalArgumentStatement(eventLine.Parameters.GetItem(3 + index, "")));
                        } // dwProp
                    }

                    return true;
                } //Parse
            } //ExternalStatement
        } //Properties
    } //Framework

    public static class Linker
    {
        private const string OperatorDefinition  = "&&";
        private const string OperatorLink = "&$";

        public class VariableData
        {
            /// <summary>The variable's name.</summary>
            public string Name = "";
            /// <summary>The variable's unique index, used by the linker.</summary>
            public int Index;

            public VariableData() {}

            public VariableData(string name, int index)
            {
                this.Name = name;
                this.Index = index;
            } //Constructor
        } //VariableData class

        public class VariableDataCollection : List<VariableData>
        {
            public int FindIndex(string name)
            {
                if(base.Count > 0) {
                    for(int index = 0; index < base.Count; index++) {
                        if(string.Equals(name, base[index].Name, System.StringComparison.CurrentCultureIgnoreCase)) return index;
                    } //index
                }

                return -1;
            } //FindIndex

            public VariableData Add(string name)
            {
                int index = FindIndex(name);
                if(index < 0) {
                    base.Add(new VariableData(name, base.Count + 1));
                    return base[base.Count - 1];
                } else
                    return base[index];
            } //Add

            internal bool ParseLine(string line)
            {
                // if(some data exist in the line then ...
                if(line.Length > 0) {
                    int position = 1;
                    do {
                        ParseVariableResults foundResults = ParseVariableString(line, position);

                        if(foundResults.IsValid) {
                            this.Add(foundResults.Name);
                            position = (foundResults.StartPosition + foundResults.PositionLength);
                        } else {
                            return true;
                        }
                    } while(true);
                }

                return false;
            } //ParseLine

            internal string ApplyToLine(string line)
            {
                string results = line;

                if(base.Count > 0) {
                    for(int index = 0; index < base.Count; index++) {
                        results = results.Replace(OperatorDefinition + base[index].Name, OperatorDefinition + "#" + base[index].Index);
                        results = results.Replace(OperatorLink + base[index].Name, OperatorLink + "#" + base[index].Index);
                    } //index
                }

                return results;
            } //ApplyToLine

            /// <summary>Tests if a given expression syntax is a function.</summary>
            /// <param name="expression">The expression syntax to check.</param>
            /// <param name="startAt">The position inside the expression to check.</param>
            /// <rule>All syntaxes begin with '&'.</rule>
            /// <rule>All variables expressions begin with a name, and nothing must follow after it.
            ///       What all indicates the end of a variable is either if it hits the end of the
            ///       string or it's a non-standard character. (a-z, A-Z, 0-9)</rule>
            /// <rule>All variables must not be an operator.</rule>
            private static ParseVariableResults ParseVariableString(string expression, int startAt)
            {
                // Find where the syntax is in the outside part of the expression.
                int syntaxPos = expression.IndexOf(OperatorDefinition, startAt - 1);
                // if(the syntax character was not found then ...
                if(syntaxPos < 0) {
                    // Find where the syntax is in the outside part of the expression.
                    syntaxPos = expression.IndexOf(OperatorLink, startAt - 1);
                    // if(the syntax character was not found then ... exit this procedure.
                    if(syntaxPos < 0) return new ParseVariableResults();
                }

                // Find the end of the variable's name ...
                int endPos = Parser.Syntax.FindEndOfSyntax(expression, 1 + syntaxPos + OperatorDefinition.Length, true, true, true, true, true, false);

                // Parse the information.
                ParseVariableResults results = new ParseVariableResults();
                results.IsValid = true;
                results.StartPosition = 1 + syntaxPos;
                results.PositionLength = (endPos - (1 + syntaxPos));
                string strName = expression.Substring(syntaxPos + OperatorDefinition.Length, endPos - (1 + syntaxPos + OperatorDefinition.Length));

                if(!string.IsNullOrEmpty(strName)) {
                    endPos = Parser.Syntax.FindEndOfSyntax(strName, 1, true, true, true, true, false, false);

                    if(endPos <= strName.Length) {
                        results.Name = strName.Substring(0, endPos - 1);
                        results.ExtraData = strName.Substring(endPos, strName.Length - endPos);
                    } else {
                        results.Name = strName;
                    }
                }

                // return the results.
                return results;
            } //ParseVariableString

            private class ParseVariableResults
            {
                public bool   IsValid;
                public int    StartPosition;
                public int    PositionLength;
                public string Name = "";
                public string ExtraData = "";
            } //ParseVariableResults class
        } //VariableDataCollection

        public class Data
        {
            public VariableDataCollection Variables = new VariableDataCollection();

            public bool Parse(Framework.Script script)
            {
                // First clear the linker data.
                this.Variables.Clear();

                // if(there are some events then ...
                if(script.Events.Count > 0) {
                    // ... Go through each event ...
                    for(int index = 0; index < script.Events.Count; index++) {
                        // ... Parse that event's parameters, if failed then ... exit this procedure.
                        if(!this.ParseEventParameters(script.Events[index].Parameters)) return false;
                    } // dwEvent
                }

                // if(there are some pointers then ...
                if(script.Pointers.Count > 0) {
                    // ... Go through each pointer ...
                    for(int index = 0; index < script.Pointers.Count; index++) {
                        // ... Parse that pointer's parameters, if failed then ... exit this procedure.
                        if(!this.ParseEventParameters(script.Pointers[index].Parameters)) return false;
                    } // dwPointer
                }

                return true;
            } //CreateData

            private bool ParseEventParameters(Framework.ParametersCollection parameters)
            {
                // if(there are some parameters then ...
                if(parameters.Count > 0) {
                    // ... Go through each parameter ...
                    for(int index = 0; index < parameters.Count; index++) {
                        // ... if(this parameter is of a value type the ...
                        if(parameters[index].Type == Framework.EventParameterTypes.Value) {
                            // ... Parse the parameter's value, if failed then ... exit this procedure.
                            if(!this.Variables.ParseLine(parameters[index].Value)) return false;
                        }
                    } //index
                }

                // return successful.
                return true;
            } //ParseEventParameters

            public bool ApplyTo(Framework.Script script)
            {
                // if(there are some linked variables then ...
                if(this.Variables.Count > 0) {
                    // ... if(there are some events then ...
                    if(script.Events.Count > 0) {
                        for(int index = 0; index < script.Events.Count; index++) {
                            if(!this.ApplyToEventParamaters(script.Events[index].Parameters)) return false;
                        } //index
                    }

                    // ... if(there are some pointers then ...
                    if(script.Pointers.Count > 0) {
                        for(int index = 0; index < script.Pointers.Count; index++) {
                            if(!this.ApplyToEventParamaters(script.Pointers[index].Parameters)) return false;
                        } //index
                    }
                }

                // return successful.
                return true;
            } //ApplyTo

            private bool ApplyToEventParamaters(Framework.ParametersCollection parameters)
            {
                // if(there are some parameters then ...
                if(parameters.Count > 0) {
                    // ... Go through each parameter ...
                    for(int index = 0; index < parameters.Count; index++) {
                        // ... if(this parameter is of a value type the ...
                        if(parameters[index].Type == Framework.EventParameterTypes.Value) {
                            // ... Store the new data.
                            parameters[index].Value = this.Variables.ApplyToLine(parameters[index].Value);
                        }
                    } //index
                }

                // return successful.
                return true;
            } //ApplyToEventParamaters
        } //Data class
    } //Linker class

    public static class Errors
    {
        public const string ERR_INVALIDARGSCOUNT = "(invalid arguments count)";
        public const string ERR_INVALIDARGSDATA  = "(invalid argument data %n)";
        public const string ERR_CMDNOTFOUND      = "(syntax not found)";
        //public const string ERR_VARNOTFOUND      = "(variable not found)";
        public const string ERR_FILENOTALLOWED   = "(file operations not allowed)";
        public const string ERR_FUNCTIONNOTFOUND = "(function not found)";
    } //Errors

    public static class Tools
    {
        public static bool IsNumeric(string value)
        {
            if(string.IsNullOrEmpty(value)) return false;
            for(int index = 0; index < value.Length; index++) {
                if(!char.IsNumber(value[index])) return false;
            }
            return true;
        } //IsNumeric
    } //Tools

    public static class Runtime
    {
#region "Constants"
        public enum FunctionAccessChecks { All = 0x0, Private = 0x1, Public = 0x2, Global = 0x4 }

        private const string SyntaxSpecialOperator = "&%";
        private const string SyntaxVariableOperator = "&&";
#endregion //Constants

        // This class handles the current DSSL environment, which includes
        // the management of variables, scripts, and the memory table.
        public class Environment
        {
            // private members:
            private Variables   mVariables = new Variables();
            private Scripts     mScripts   = new Scripts();

            // Returns the private variables class.
            public Variables Variables { get { return this.mVariables; } }

            // Returns the private scripts class.
            public Scripts Scripts { get { return this.mScripts; } }
        } //Environment

        // The variables class, this manages a private collection of variables.
        public class Variables : List<Variable>
        {
            public enum LocationTypes { NotSet, Global, Public, Locale }

            internal MemoryEntries mMemoryTable = new MemoryEntries();

            public Variables()
            {
                this.Clear();
            } //Constructor

#region "Shared Functions"
            public static LocationTypes ScopeToLocationType(Parser.VariableScopes scopeType)
            {
                switch(scopeType) {
                    case(Parser.VariableScopes.Global):  return LocationTypes.Global;
                    case(Parser.VariableScopes.Public):  return LocationTypes.Public;
                    case(Parser.VariableScopes.Private): return LocationTypes.Locale;
                    default:                             return LocationTypes.NotSet;
                }
            } //ScopeToLocationType function

            public static string VariableLocationTypeToString(LocationTypes locationType, string locationParent = "")
            {
                switch(locationType) {
                    case(LocationTypes.NotSet): return "(Not Set)";
                    case(LocationTypes.Global): return "Global";
                    case(LocationTypes.Public): return "Public" + (string.IsNullOrEmpty(locationParent) ? "" : "(" + locationParent + ")");
                    case(LocationTypes.Locale): return "Locale" + (string.IsNullOrEmpty(locationParent) ? "" : "(" + locationParent + ")");
                    default:                    return "(Not Set)";
                }
            } //VariableLocationTypeToString function

            public static string MakeLocationID(LocationTypes locationType = LocationTypes.Global, string locationParent = "")
            {
                switch(locationType) {
                    case(LocationTypes.NotSet): return "-1";
                    case(LocationTypes.Global): return ""; 
                    case(LocationTypes.Public): return "PUBLIC:" + locationParent; 
                    case(LocationTypes.Locale): return "LOCALE:" + locationParent; 
                    default:                    return "";
                }
            } //MakeLocationID function

            public static string DetermineLocationID(LocationTypes targetLocationType, string localeLocationID, string publicLocationID, out LocationTypes outLocationType, out string outLocationParent)
            {
                string locationID = "";
                switch(targetLocationType) {
                    case(LocationTypes.Public): locationID = publicLocationID; break;
                    case(LocationTypes.Locale): locationID = localeLocationID; break;
                    default:                    locationID = MakeLocationID(targetLocationType); break;
                }

                SplitLocationID(locationID, out outLocationType, out outLocationParent);
                return locationID;
            } //DetermineLocationID function

            public static void SplitLocationID(string locationID, out LocationTypes outLocationType, out string outLocationParent)
            {
                outLocationType   = LocationTypes.NotSet;
                outLocationParent = "";

                if(string.Equals(locationID, "-1"))
                    outLocationType = LocationTypes.NotSet;
                else if(string.IsNullOrEmpty(locationID))
                    outLocationType = LocationTypes.Global;
                else if(locationID.StartsWith("PUBLIC:", System.StringComparison.CurrentCultureIgnoreCase)) {
                    outLocationType = LocationTypes.Public; 
                    outLocationParent = locationID.Substring("PUBLIC:".Length);
                } else if(locationID.StartsWith("LOCALE:", System.StringComparison.CurrentCultureIgnoreCase)) {
                    outLocationType = LocationTypes.Locale; 
                    outLocationParent = locationID.Substring("LOCALE:".Length);
                }
            } //SplitLocationID function
#endregion //Shared Functions

            internal bool IsIndexValid(int index, bool checkIfEntryValid = true)
            {
                if(index < 1 || index > base.Count) return false;
                if(checkIfEntryValid && !base[index - 1].IsUsed) return false;
                return true;
            } //IsIndexValid

            public int FindIndex(string name, string localeLocationID, string publicLocationID, bool allowGlobalSearching = true, bool allowPublicSearching = true)
            {
                name = Parser.Syntax.FixVariableName(name);
                if(string.IsNullOrEmpty(name) || base.Count == 0) return 0;

                // Split up the location IDs.
                Variables.LocationTypes[] locationTypes = new Variables.LocationTypes[2];
                string[] locationParents = new string[2];
                Variables.SplitLocationID(publicLocationID, out locationTypes[0], out locationParents[0]);
                Variables.SplitLocationID(localeLocationID, out locationTypes[1], out locationParents[1]);

                // Find the private variable (at current location)
                int variableIndex = this.FindIndexByLocation(name, locationTypes[1], locationParents[1]);

                if(variableIndex >= 0)
                    return variableIndex;
                else {
                    if(allowPublicSearching) {
                        // ... Find the public variable ...
                        variableIndex = this.FindIndexByLocation(name, locationTypes[0], locationParents[0]);
                        if(variableIndex >= 0) return variableIndex;
                    }

                    if(allowGlobalSearching) {
                        // ... Find the global variable ...
                        variableIndex = this.FindIndexByLocation(name, Variables.LocationTypes.Global);
                        if(variableIndex >= 0) return variableIndex;
                    }
                }

                return -1;
            } //FindIndex

            public int FindIndexByLocation(string name, LocationTypes locationType = LocationTypes.Global, string locationParent = "")
            {
                if(name.StartsWith("#", System.StringComparison.CurrentCultureIgnoreCase)) {
                    string value = name.Substring(1);
                    if(Tools.IsNumeric(value)) {
                        int index = System.Convert.ToInt32(value);
                        if(this.IsIndexValid(index) && base[index - 1].DoesItemMatch(base[index - 1].Name, locationType, locationParent)) {
                            return (index - 1);
                        } else {
                            return -1;
                        }
                    }
                }

                if(base.Count > 0) {
                    for(int index = 0; index < base.Count; index++) {
                        if(this[index].DoesItemMatch(name, locationType, locationParent)) return index;
                    } //index
                }

                return -1;
            } //FindIndexByLocation

            public Variable FindByMemoryEntry(MemoryEntry entry)
            {
                if(base.Count > 0) {
                    for(int index = 0; index < base.Count; index++) {
                        if(base[index].IsUsed && object.ReferenceEquals(base[index].Value, entry)) return base[index];
                    } //index
                }

                return null;
            } //FindByMemoryEntry

            public Variable FindByLocation(string name, LocationTypes locationType = LocationTypes.Global, string locationParent = "")
            {
                int index = this.FindIndexByLocation(name, locationType, locationParent);
                if(index < 0) return null;
                return base[index];
            } //FindByLocation

            public Variable Add(string name, bool isReadOnly = false, bool canDelete = true, LocationTypes locationType = 0, string locationParent = "")
            {
                Variable item = FindByLocation(name, locationType, locationParent);
                if(item == null) {
                    item = new Variable();
                    item.Name = name;
                    base.Add(item);
                }

                item.IsUsed = true;
                item.LocationType = locationType;
                item.LocationParent = locationParent;
                item.Value = null;
                item.IsReadOnly = isReadOnly;
                item.CanDelete = canDelete;

                return base[base.Count - 1];

            } //Add
            
            public Variable Add(string name, MemoryEntry valueEntry, bool isReadOnly = false, bool canDelete = true, LocationTypes locationType = 0, string locationParent = "")
            {
                Variable item = FindByLocation(name, locationType, locationParent);
                if(item == null) {
                    item = new Variable();
                    item.Name = name;
                    base.Add(item);
                }

                item.IsUsed         = true;
                item.LocationType   = locationType;
                item.LocationParent = locationParent;
                item.Value          = valueEntry;
                item.IsReadOnly     = isReadOnly;
                item.CanDelete      = canDelete;

                return base[base.Count - 1];
            } //Add

#region "Managed Properties"
            public Variable Add(string name, string value = "", bool isReadOnly = false, bool canDelete = true, Variables.LocationTypes targetLocationType = 0, string linkName = "", int arraySize = 0, string localeLocationID = "", string publicLocationID = "", string linkSearchLocationID = "")
            {
                // Fix the variable's name.
                name = Parser.Syntax.FixVariableName(name);

                // Make up the location data.
                string locationID = "", locationParent = "";
                Variables.LocationTypes locationType;
                if(targetLocationType == Variables.LocationTypes.Public) {
                    locationID = publicLocationID;
                } else if(targetLocationType == Variables.LocationTypes.Locale) {
                    locationID = localeLocationID;
                } else {
                    locationID = Variables.MakeLocationID(targetLocationType);
                }
                Variables.SplitLocationID(locationID, out locationType, out locationParent);

                MemoryEntry memoryAddress = null;
                if(string.IsNullOrEmpty(linkName)) {
                    // Get information about the value.
                    List<string> arrayValues = Parser.Syntax.ParseArrayValueData(value);

                    if(arraySize == 0) {
                        string linkVariableName = "", linkArrayIndex = ""; ;
                        if(Parser.Syntax.IsLinkTag(arrayValues[0], out linkVariableName)) {
                            Parser.Syntax.ParseArrayNameData(linkVariableName, out linkVariableName, out linkArrayIndex);
                        }

                        if(string.IsNullOrEmpty(linkVariableName)) {
                            memoryAddress = this.mMemoryTable.AddValue(arrayValues[0]);
                        } else {
                            int targetVariableIndex = this.FindIndex(linkVariableName, locationID, publicLocationID, true, true);
                            if(targetVariableIndex >= 0) {
                                memoryAddress = base[targetVariableIndex].Value;
                                memoryAddress = memoryAddress.GetArrayPointer(System.Convert.ToInt32(linkArrayIndex));
                            }
                            if(memoryAddress == null) memoryAddress = this.mMemoryTable.AddValue(arrayValues[0]);
                        }
                    } else {
                        memoryAddress = this.mMemoryTable.AddValue("");
                        memoryAddress.ResizeArraySafe(arraySize, this, false, "");

                        string strNewValue = "", linkVariableName = "", strLinkArrayIndex = "";
                        for(int arrayItem = 1; arrayItem <= arraySize; arrayItem++) {
                            strNewValue = "";

                            if(arrayValues == null || arrayValues.Count == 0) {
                                strNewValue = value;
                            } else if(arrayValues.Count == 1) {
                                strNewValue = arrayValues[0];
                            } else if(arrayItem <= arrayValues.Count) {
                                strNewValue = arrayValues[arrayItem - 1];
                            }

                            if(Parser.Syntax.IsLinkTag(strNewValue, out linkVariableName)) {
                                Parser.Syntax.ParseArrayNameData(linkVariableName, out linkVariableName, out strLinkArrayIndex);
                            }

                            if(string.IsNullOrEmpty(linkVariableName)) {
                                memoryAddress.SetArrayPointer(arrayItem, this.mMemoryTable.AddValue(strNewValue));
                            } else {
                                int targetVariableIndex = this.FindIndex(linkVariableName, localeLocationID, publicLocationID, true, true);
                                if(targetVariableIndex >= 0) {
                                    memoryAddress.SetArrayPointer(arrayItem, base[targetVariableIndex].Value.GetArrayPointer(System.Convert.ToInt32(strLinkArrayIndex)));
                                } else {
                                    memoryAddress.SetArrayPointer(arrayItem, this.mMemoryTable.AddValue(strNewValue));
                                }
                            }
                        } // dwArrayItem
                    }
                }

                Variable newItem = this.Add(name, memoryAddress, isReadOnly, canDelete, locationType, locationParent);
                if(!string.IsNullOrEmpty(linkName)) {
                    Variables.SplitLocationID(linkSearchLocationID, out locationType, out locationParent);
                    Variable linkedItem = this.FindByLocation(linkName, locationType, locationParent);
                    this.Link(newItem, 0, linkedItem, 0);
                }
                return newItem; //this.mParent[count - 1]
            } //Add function

            public bool Remove(string name, string location, bool ignoreIfCantDelete = false, string publicLocation = "", bool clearData = true, bool clearValue = true, bool removeEntry = false)
            {
                // Find the private variable (at current location)
                int variableIndex = this.FindIndex(name, location, publicLocation, true, true);
                if(variableIndex < 0) return false;

                return this.Remove(1 + variableIndex, ignoreIfCantDelete, clearData, clearValue, removeEntry);
            } //Remove

            public bool Remove(int index, bool ignoreIfCantDelete = false, bool clearData = true, bool clearValue = true, bool removeEntry = false)
            {
                // if(the index is not valid then ... exit this procedure.
                if(!this.IsIndexValid(index)) return false;

                // if(this variable cannot be deleted then ... exit this procedure.
                if(!ignoreIfCantDelete && !base[index - 1].CanDelete) return false;

                // Clear this variable's data.
                if(clearData) {
                    base[index - 1].IsUsed = false;
                    base[index - 1].CanDelete = true; //False
                    base[index - 1].IsReadOnly = false;
                    base[index - 1].Name = "";
                }
                base[index - 1].LocationType = Variables.LocationTypes.NotSet;
                base[index - 1].LocationParent = "";

                if(clearValue) {
                    if(base[index - 1].Value != null) {
                        MemoryEntry pointer = base[index - 1].Value;
                        base[index - 1].Value = null;
                        pointer.DestorySafe(this);
                    }
                }

                if(removeEntry) this.RemoveAt(index - 1);

                // return successful.
                return true;
            } //Remove

            public void RemoveAllByLocation(Variables.LocationTypes locationType, string locationParent)
            {
                if(base.Count == 0) return;

                // ... Go through each variable that exists ...
                for(int index = (base.Count - 1); index >= 0; index -= 1) {
                    // ... if(this variable's location ID matches what we are looking for, then ...
                    if(base[index].LocationType == locationType && string.Equals(base[index].LocationParent, locationParent)) {
                        // ... Safely remove the variable.
                        this.Remove(1 + index, false, false, true);
                    }
                } //index
            } //RemoveAllByLocation

            public void Link(string name, int arrayIndex, string targetName, string targetLocationID, int targetArrayIndex, string localeLocationID = "", string publicLocationID = "")
            {
                // Find the private variable (at current location)
                int variableIndex = this.FindIndex(name, localeLocationID, publicLocationID, true, true);

                // ... Find the target variable ...
                int targetVariableIndex = -1;
                if(!string.IsNullOrEmpty(targetName)) {
                    targetVariableIndex = this.FindIndex(targetName, targetLocationID, publicLocationID, true, true);
                }

                this.Link(1 + variableIndex, arrayIndex, 1 + targetVariableIndex, targetArrayIndex);
            } //Link

            public void Link(int sourceVariableIndex, int arrayIndex, int targetVariableIndex, int targetArrayIndex)
            {
                if(!this.IsIndexValid(sourceVariableIndex)) return;
                this.Link(base[sourceVariableIndex - 1], arrayIndex, base[targetVariableIndex - 1], targetArrayIndex);
            } //Link

            public void Link(Variable sourceVariable, int arrayIndex, Variable targetVariable, int targetArrayIndex)
            {
                if(sourceVariable == null) return;

                // First backup the old link ...
                MemoryEntry oldPointer = sourceVariable.Value;
                sourceVariable.Value = null;
                // Second remove the old link ...
                if(oldPointer != null) {
                    // ... Find information about the array ...
                    MemoryEntry arrayPointer = oldPointer.GetArrayPointer(arrayIndex);
                    // ... Safely destory the entry ...
                    if(arrayPointer == null) {
                        oldPointer.DestorySafe(this);
                    } else {
                        arrayPointer.DestorySafe(this);
                    }
                    // ... Clear the temporary address pointer.
                    oldPointer = null;
                }

                // if(we should create a new memory location for the variable then ...
                if(sourceVariable == null) {
                    // ... Create a new memory address for the current variable.
                    sourceVariable.Value = this.mMemoryTable.AddValue("");
                    // if(we should link the variable to the new address then ...
                } else {
                    // ... Find information about the array ...
                    MemoryEntry arrayPointer = targetVariable.Value.GetArrayPointer(targetArrayIndex);
                    // ... Copy the target address to the current variable.
                    sourceVariable.Value = (arrayPointer == null ? targetVariable.Value : arrayPointer);
                }
            } //Link

            public void Unlink(string name, string location, string publicLocationID = "")
            {
                this.Link(name, 0, "", "", 0, location, publicLocationID);
            } //Unlink

            public int GetArraySize(int index)
            {
                // if(the index is out of range then ... exit this procedure.
                if(!this.IsIndexValid(index)) return 0;

                // Get the variable's address.
                MemoryEntry pointer = base[index - 1].Value;
                if(pointer == null) return 0;

                // return the array count from memory.
                return pointer.GetArrayCount();
            } //GetArraySize

            public bool SetArraySize(int index, int newSize, bool keepContents = false, string defaultEntryValue = "")
            {
                // if(the index is out of range then ... exit this procedure.
                if(!this.IsIndexValid(index)) return false;

                // Get the variable's address.
                MemoryEntry pointer = base[index - 1].Value;
                if(pointer == null) return false;

                // Resize the array and return the results.
                return pointer.ResizeArraySafe(newSize, this, keepContents, defaultEntryValue);
            } //SetArraySize

            public string GetValue(string name, string location, string defaultValue = "", string publicLocation = "", int arrayIndex = 0)
            {
                // Find the private variable (at current location)
                int variableIndex = this.FindIndex(name, location, publicLocation, true, true);
                if(variableIndex < 0) return defaultValue;
                return base[variableIndex].GetValue(defaultValue, arrayIndex);
            } //GetValue

            public string GetValue(int index, string defaultValue = "", int arrayIndex = 0)
            {
                if(!this.IsIndexValid(index)) return defaultValue;
                return base[index - 1].GetValue(defaultValue, arrayIndex);
            } //GetValue

            public Variable SetValue(string name, string value, int arrayIndex = 0, string localeLocationID = "", string publicLocationID = "")
            {
                // Fix the variable name.
                name = Parser.Syntax.FixVariableName(name);

                int variableIndex = this.FindIndex(name, localeLocationID, publicLocationID, true, true);

                // if(the variable was found then ...
                if(variableIndex >= 0) {
                    // ... Fix the variable's location ...
                    base[variableIndex].SetLocationID(localeLocationID, true);

                    // Get information about the value.
                    List<string> arrayValues = Parser.Syntax.ParseArrayValueData(value);

                    // if(this variable is a link then ...
                    string strLinkVar = "";
                    if(Parser.Syntax.IsLinkTag(arrayValues[0], out strLinkVar)) {
                        string strLinkArrayIndex = "";
                        Parser.Syntax.ParseArrayNameData(strLinkVar, out strLinkVar, out strLinkArrayIndex);

                        int linkVariableIndex = this.FindIndex(strLinkVar, localeLocationID, publicLocationID, true, true);
                        this.Link(1 + variableIndex, arrayIndex, 1 + linkVariableIndex, System.Convert.ToInt32(strLinkArrayIndex));
                    } else {
                        SetValue(1 + variableIndex, value, arrayIndex);
                    }

                    return base[variableIndex];
                } else { // if(the variable was not found then ...
                    // ... Split the location ID ...
                    Variables.LocationTypes eLocationType = Variables.LocationTypes.NotSet;
                    string strLocationParent = "";
                    Variables.SplitLocationID(localeLocationID, out eLocationType, out strLocationParent);
                    return this.Add(name, value, false, true, eLocationType, "", arrayIndex, localeLocationID, publicLocationID);
                }
            } //SetValue

            public bool SetValue(int index, string newValue, int arrayIndex = 0, bool ignoreReadOnly = false)
            {
                // if(the index is valid then ... exit this procedure.
                if(!this.IsIndexValid(index)) return false;
                return base[index - 1].SetValue(newValue, this, arrayIndex, ignoreReadOnly);
            } //SetValue
#endregion //Managed Properties
        } //Variables

        public class Variable
        {
            /// <summary>If the variable was safely deleted, this iwll be set to false.</summary>
            public bool IsUsed;
            /// <summary>Where the variable is accessible at.</summary>
            public Variables.LocationTypes LocationType;
            /// <summary>The variable's location's parent (or sub-parent.)</summary>
            public string LocationParent;
            /// <summary>The variable's unique name.</summary>
            public string Name;
            /// <summary>The variable's address in the memory table.</summary>
            public MemoryEntry Value;
            /// <summary>Enables/disables if the variable's contents can be changed.</summary>
            public bool IsReadOnly;
            /// <summary>Enables/disables if the variable can be deleted by the user.</summary>
            public bool CanDelete;

            public bool DoesLocationMatch(Variables.LocationTypes locationType = Variables.LocationTypes.Global, string locationParent = "")
            {
                if(this.LocationType == Variables.LocationTypes.NotSet) {
                    return true;
                } else {
                    if(this.LocationType == locationType) {
                        if(locationType == Variables.LocationTypes.Public || locationType == Variables.LocationTypes.Locale) {
                            if(this.LocationParent == locationParent) return true;
                        } else {
                            return true;
                        }
                    }
                }

                return false;
            } //DoesLocationMatch

            public bool DoesItemMatch(string nameToCheck, Variables.LocationTypes locationType = Variables.LocationTypes.Global, string locationParent = "")
            {
                if(!this.DoesLocationMatch(locationType, locationParent)) return false;
                if(!string.Equals(this.Name, nameToCheck, System.StringComparison.CurrentCultureIgnoreCase)) return false;
                return true;
            } //DoesItemMatch

            public void SetLocation(Variables.LocationTypes locationType = Variables.LocationTypes.Global, string locationParent = "", bool setOnlyIfNotSet = false)
            {
                if(setOnlyIfNotSet) {
                    if(this.LocationType == Variables.LocationTypes.NotSet) {
                        this.LocationType = locationType;
                        this.LocationParent = locationParent;
                    }
                } else {
                    this.LocationType = locationType;
                    this.LocationParent = locationParent;
                }
            } //SetLocation

            public void SetLocationID(string locationID = "", bool setOnlyIfNotSet = false)
            {
                Variables.LocationTypes locationType;
                string locationParent = "";
                Variables.SplitLocationID(locationID, out locationType, out locationParent);
                this.SetLocation(locationType, locationParent, setOnlyIfNotSet);
            } //SetLocationID

            public string GetLocationID() { return Variables.MakeLocationID(this.LocationType, this.LocationParent); }

#region "Managed Code"
            public string GetValue(string defaultValue = "", int arrayIndex = 0)
            {
                if(!this.IsUsed) return defaultValue;

                // Get the variable's memory address.
                if(this.Value == null) return defaultValue;

                // Fix the memory address just in case we're looking at an array.
                MemoryEntry pointer = this.Value.GetArrayPointer(arrayIndex);
                if(pointer == null) return defaultValue;

                // return the value.
                return pointer.Value;
            } //GetValue

            public bool SetValue(string newValue, Variables variables, int arrayIndex = 0, bool ignoreReadOnly = false)
            {
                // if(the index is valid then ... exit this procedure.
                if(!this.IsUsed) return false;
                // if(the variable is read-only (variable can't be changed) then ... exit this procedure.
                if(!ignoreReadOnly && this.IsReadOnly) return false;

                // if the address is valid then ...
                if(this.Value != null && this.Value.IsUsed) {
                    MemoryEntry newPointer = this.Value.GetArrayPointer(arrayIndex);
                    if(newPointer != null) {
                        newPointer.Value = newValue;
                    } else {
                        this.Value.ResizeArraySafe(arrayIndex, variables, true, "");
                        newPointer = this.Value.GetArrayPointer(arrayIndex);
                        if(newPointer != null) {
                            newPointer.Value = newValue;
                        } else {
                            this.Value.Value = newValue;
                        }
                    }
                    // if(the address is not valid then we need to create a new address ...
                } else {
                    if(arrayIndex <= 0) {
                        this.Value = variables.mMemoryTable.AddValue(newValue);
                    } else {
                        string strValues = "";
                        for(int dwValue = 1; dwValue <= arrayIndex; dwValue++) {
                            strValues = (string.IsNullOrEmpty(strValues) ? "" : strValues + ",") + (dwValue == arrayIndex ? newValue : "");
                        } // dwValue
                        this.Value = variables.mMemoryTable.AddArray(strValues);
                    }
                }

                // return successful.
                return true;
            } //SetValue
#endregion //Managed Code
        } //Variable

        // The scripts class, this manages the runtime scripts collection.
        public class Scripts : List<Script>
        {
            private bool IsIndexValid(int index) { return (index >= 1 && index <= base.Count); }

            public Script Add(Framework.Script script, string defaultScriptID = "", string key = "")
            {
                base.Add(new Script(key, script, defaultScriptID));
                return base[base.Count - 1];
            } //Add

            public Script Add(string fileName = "", string defaultScriptID = "", string key = "")
            {
                // First make sure the script doesn't already exist, if it does return that instead of adding a new one.
                int index = this.FindIndexByFileName(fileName);
                if(index >= 0) return base[index];

                Framework.Script dScript = new Framework.Script();

                if(System.IO.File.Exists(fileName)) {
                    switch(System.IO.Path.GetExtension(fileName).ToLower()) {
                        case(".dcs"):
                            if(!dScript.LoadFromFile(fileName)) return null;
                            break;
                        case(".drs"):
                            string strScriptID = "";
                            dScript = RapidCodeParser.ParseFromFile(fileName, out strScriptID);
                            if(dScript == null) return null;
                            if(!string.IsNullOrEmpty(strScriptID)) defaultScriptID = strScriptID;
                            break;
                    }
                }

                return this.Add(dScript, defaultScriptID, key);
            } //Add

            public int FindIndex(string scriptID)
            {
                if(base.Count > 0) {
                    for(int index = 0; index < base.Count; index++) {
                        if(string.Equals(base[index].ID, scriptID, System.StringComparison.CurrentCultureIgnoreCase)) return index;
                    } // dwIndex
                }

                return -1;
            } //FindIndex

            private int FindIndexByKey(string key)
            {
                if(base.Count > 0) {
                    for(int index = 0; index < base.Count; index++) {
                        if(string.Equals(base[index].Key, key, System.StringComparison.CurrentCultureIgnoreCase)) return index;
                    } // dwIndex
                }

                return -1;
            } //FindIndexByKey

            private int FindIndexByFileName(string fileName)
            {
                if(base.Count > 0) {
                    for(int index = 0; index < base.Count; index++) {
                        if(string.Equals(base[index].Desc.FileName, fileName, System.StringComparison.CurrentCultureIgnoreCase)) return index;
                    } // dwIndex
                }

                return -1;
            } //FindIndexByFileName

            /// <summary>Searches each script for the specified function</summary>
            /// <param name="callName">The function name to find, in call name syntax (ParentName.FunctionName).</param>
            /// <param name="currentScript">The script with the precedence in the search.</param>
            /// <param name="outScript">The script which contained the function.</param>
            /// <returns>The first function with the name found.</returns>
            public Script.ParsedFunction FindFunction(string callName, Script currentScript, out Script outScript)
            {
                outScript = null;

                string parentName = "", functionName = "";
                Parser.Syntax.ParseFunctionCallName(callName, out parentName, out functionName);

                if(string.IsNullOrEmpty(parentName)) {
                    // Step 1, Search inside the home script for this script.
                    if(currentScript != null) {
                        Script.ParsedFunction function = currentScript.Parsed.Functions.Find(functionName);
                        if(function != null) {
                            outScript = currentScript;
                            return function;
                        }
                    }

                    // Step 2, Search all scripts for a global function.
                    if(base.Count > 0) {
                        Script.ParsedFunction function = null;
                        for(int scriptIndex = 0; scriptIndex < base.Count; scriptIndex++) {
                            function = base[scriptIndex].Parsed.Functions.Find(functionName, Runtime.FunctionAccessChecks.Global);
                            if(function != null) {
                                outScript = base[scriptIndex];
                                return function;
                            }
                        } // dwScript
                    }
                } else {
                    if(base.Count > 0) {
                        Script.ParsedFunction function = null;
                        // ... Go through each script ...
                        for(int scriptIndex = 0; scriptIndex < base.Count; scriptIndex++) {
                            // ... if(the script ID matches then ...
                            if(string.Equals(base[scriptIndex].ID, parentName, System.StringComparison.CurrentCultureIgnoreCase)) {
                                function = base[scriptIndex].Parsed.Functions.Find(functionName, Runtime.FunctionAccessChecks.Global | Runtime.FunctionAccessChecks.Public);
                                if(function != null) {
                                    outScript = base[scriptIndex];
                                    return function;
                                }
                            }
                        } // dwScript
                    }
                }

                if(base.Count > 0) {
                    Script.ParsedFunction function = null;
                    // ... Go through each script ...
                    for(int scriptIndex = 0; scriptIndex < base.Count; scriptIndex++) {
                        function = base[scriptIndex].Parsed.Functions.Find(callName, Runtime.FunctionAccessChecks.Global | Runtime.FunctionAccessChecks.Public);
                        if(function != null) {
                            outScript = base[scriptIndex];
                            return function;
                        }
                    } // dwScript
                }

                return null;
            } //FindFunction function

            public Script this[string id]
            {
                get {
                    int index = this.FindIndex(id);
                    if(index < 0) return null;
                    return base[index];
                }
            } //Item

            public Script FindByKey(string key)
            {
                int index = this.FindIndexByKey(key);
                if(index < 0) return null;
                return base[index];
            } //FindByKey

            public Script FindByFileName(string fileName)
            {
                int index = this.FindIndexByFileName(fileName);
                if(index < 0) return null;
                return base[index];
            } //FindByFileName
        } //Scripts

        public class Script
        {
#region "Parsed Data"
            public class EventIndexes : List<int> {}

            public class ParsedIf
            {
                public int          StartEventIndex;
                public EventIndexes PartEventIndexes = new EventIndexes();
                public int          EndEventIndex;

                public ParsedIf() { }

                public ParsedIf(int startEventIndex) { this.StartEventIndex = startEventIndex; }
            } //ParsedIf

            public class ParsedIfsCollection : List<ParsedIf> {}

            public class ParsedFunction
            {
                public int EventIndex;
                public int EndEventIndex;
                public Framework.Properties.FunctionStatement Desc;

                public bool CheckAccess(Runtime.FunctionAccessChecks accessCheck = FunctionAccessChecks.All)
                {
                    if(accessCheck == Runtime.FunctionAccessChecks.All) {
                        return true;
                    } else {
                        if(this.Desc.Access == Parser.FunctionAccess.Private && (accessCheck & Runtime.FunctionAccessChecks.Private) == Runtime.FunctionAccessChecks.Private) {
                            return true;
                        } else if(this.Desc.Access == Parser.FunctionAccess.Public && (accessCheck & Runtime.FunctionAccessChecks.Public) == Runtime.FunctionAccessChecks.Public) {
                            return true;
                        } else if(this.Desc.Access == Parser.FunctionAccess.Global && (accessCheck & Runtime.FunctionAccessChecks.Global) == Runtime.FunctionAccessChecks.Global) {
                            return true;
                        }

                        return false;
                    }
                } //CheckAccess
            } //ParsedFunction

            public class ParsedFunctionsCollection : List<ParsedFunction>
            {
                public int FindIndex(string name, Runtime.FunctionAccessChecks accessCheck = Runtime.FunctionAccessChecks.All)
                {
                    if(base.Count > 0) {
                        for(int index = 0; index < base.Count; index++) {
                            if(base[index].CheckAccess(accessCheck) && string.Equals(base[index].Desc.Name, name, System.StringComparison.CurrentCultureIgnoreCase)) {
                                return index;
                            }
                        } // index
                    }

                    return -1;
                } //FindIndex

                public ParsedFunction Find(string name, Runtime.FunctionAccessChecks accessCheck = Runtime.FunctionAccessChecks.All)
                {
                    int index = this.FindIndex(name, accessCheck);
                    if(index < 0) return null;
                    return base[index];
                } //FindFunction
            } //ParsedFunctionsCollection

            public class ParsedData
            {
                public   ParsedFunctionsCollection Functions    = new ParsedFunctionsCollection();
                public   ParsedIfsCollection       IfStatements = new ParsedIfsCollection();
                private  Linker.Data               mLinkerData  = new Linker.Data();

                public void Parse(Framework.Script script)
                {
                    // if(we should link variables then ...
                    if(Settings.LinkVariables) {
                        // Create linker data.
                        if(this.mLinkerData.Parse(script)) {
                            // ... Fix the variables list with correct indexes with any existing variables ...
                            // (The only variables that should exist and be overrided if already exists is
                            //  global variables.)
                            if(this.mLinkerData.Variables.Count > 0) {
                                int foundVariableIndex = -1;
                                for(int variableIndex = 0; variableIndex < this.mLinkerData.Variables.Count; variableIndex++) {
                                    foundVariableIndex = Globals.MyEnvironment.Variables.FindIndexByLocation(this.mLinkerData.Variables[variableIndex].Name, Variables.LocationTypes.Global, "");
                                    if(foundVariableIndex < 0) {
                                        Variable newVariable = Globals.MyEnvironment.Variables.Add(this.mLinkerData.Variables[variableIndex].Name, false, false, Runtime.Variables.LocationTypes.NotSet);
                                        this.mLinkerData.Variables[variableIndex].Index = 1 + Globals.MyEnvironment.Variables.IndexOf(newVariable);
                                    } else {
                                        this.mLinkerData.Variables[variableIndex].Index = 1 + foundVariableIndex;
                                    }
                                } // dwVar
                            }

                            // ... Apply the linker data.
                            this.mLinkerData.ApplyTo(script);
                        }
                    }

                    // Clear any existing data.
                    this.Functions.Clear();
                    // Pre-find everything that is needed
                    if(script.Events.Count != 0) {
                        Framework.Properties.FunctionStatement dFunction = new Framework.Properties.FunctionStatement();
                        for(int eventIndex = 0; eventIndex < script.Events.Count; eventIndex++) {
                            if(script.Events[eventIndex].Command == Framework.EventCommands.Function) {
                                if(dFunction.Parse(script.Events[eventIndex])) {
                                    if(dFunction.Type == Parser.FunctionTypes.Header) {
                                        this.Functions.Add(new ParsedFunction());
                                        this.Functions[this.Functions.Count - 1].EventIndex = (1 + eventIndex);
                                        this.Functions[this.Functions.Count - 1].Desc = dFunction;
                                    } else if(dFunction.Type == Parser.FunctionTypes.End) {
                                        this.Functions[this.Functions.Count - 1].EndEventIndex = (1 + eventIndex);
                                    }
                                }
                            }
                        } // eventIndex
                    }

                    // Parse the if statements.
                    this.ParseIfStatements(script);
                } //Parse

                private void ParseIfStatements(Framework.Script script)
                {
                    // Clear any existing data.
                    this.IfStatements.Clear();

                    // Pre-find everything that is needed
                    if(script.Events.Count > 0) {
                        int ifIndex = 0, prevIfIndex = 0;
                        Framework.Properties.IfStatement dIfStatement = new Framework.Properties.IfStatement();
                        for(int eventIndex = 0; eventIndex < script.Events.Count; eventIndex++) {
                            if(script.Events[eventIndex].Command == Framework.EventCommands.IfStatement) {
                                if(dIfStatement.Parse(script.Events[eventIndex])) {
                                    if(dIfStatement.Type == Parser.IfStatementTypes.If) {
                                        this.IfStatements.Add(new ParsedIf(1 + eventIndex));
                                        prevIfIndex = ifIndex;
                                        ifIndex = this.IfStatements.Count;
                                    } else if(dIfStatement.Type == Parser.IfStatementTypes.ElseIf || dIfStatement.Type == Parser.IfStatementTypes.Else) {
                                        this.IfStatements[ifIndex - 1].PartEventIndexes.Add((1 + eventIndex));
                                    } else if(dIfStatement.Type == Parser.IfStatementTypes.EndIf) {
                                        this.IfStatements[ifIndex - 1].EndEventIndex = (1 + eventIndex);
                                        ifIndex = prevIfIndex;
                                    }
                                }
                            }
                        } // eventIndex
                    }
                } //ParseIfStatements
            } //ParsedData
#endregion //Parsed Data

            public string           ID         = "";
            public string           Key        = "";
            public Framework.Script Desc       = new Framework.Script();
            public Framework.Script OldDesc    = new Framework.Script();
            public ParsedData       Parsed     = new ParsedData();

            public Script() {}

            public Script(string key, Framework.Script script, string defaultScriptID)
            {
                this.Key = key;
                this.Set(script, defaultScriptID);
            } //Constructor

            private bool Set(Framework.Script scriptData, string defaultScriptID = "")
            {
                if(!string.IsNullOrEmpty(defaultScriptID)) this.ID = defaultScriptID;
                this.Desc    = scriptData;
                this.OldDesc = new Framework.Script(this.Desc);
                this.Parse();
                return true;
            } //Set

            private void Parse()
            {
                // Restore old script.
                this.Desc = new Framework.Script(this.OldDesc);
                this.Parsed.Parse(this.Desc);
            } //Parse

            public string MakeLocationID() { return Variables.MakeLocationID(Variables.LocationTypes.Public, this.ID); }
        } //Script

        public class MemoryEntries : List<MemoryEntry>
        {
            public MemoryEntry AddValue(string value)
            {
                int index = this.FindUnusedIndex();
                if(index < 0) {
                    base.Add(new MemoryEntry());
                    index = (base.Count - 1);
                }

                base[index].IsUsed = true;
                base[index].ArrayEntries.Clear();
                base[index].Value = value;

                return base[index];
            } //Add

            public MemoryEntry AddArray(string values)
            {
                MemoryEntry item = this.AddValue("");

                List<string> valuesList; StringManager.SmartSplit(values, ",", out valuesList);

                if(valuesList != null && valuesList.Count > 0) {
                    for(int valueIndex = 0; valueIndex < valuesList.Count; valueIndex++) {
                        item.ArrayEntries.Add(this.AddArray(valuesList[valueIndex]));
                    } // dwValue
                }

                return item;
            } //Add

            public int FindUnusedIndex()
            {
                if(base.Count > 0) {
                    for(int index = 0; index < base.Count; index++) {
                        if(!base[index].IsUsed) return index;
                    } // dwEntry
                }

                return -1;
            } //FindUnusedIndex
        } //MemoryEntries class

        public class MemoryEntry
        {
            public bool        IsUsed;
            public string      Value        = "";
            public List<MemoryEntry> ArrayEntries = new List<MemoryEntry>();

            public bool ResizeArraySafe(int newArraySize, Variables variables, bool keepContents = false, string defaultEntryValue = "")
            {
                // if(the entry is not valid then ... exit this procedure.
                if(!this.IsUsed) return false;

                // if(we're keeping the contents then ...
                if(!keepContents) {
                    // ... Safely remove all the array entries ...
                    if(this.ArrayEntries.Count > 0) {
                        for(int addressIndex = 0; addressIndex < this.ArrayEntries.Count; addressIndex++) {
                            if(this.ArrayEntries[addressIndex] != null) {
                                this.ArrayEntries[addressIndex].DestorySafe(variables);
                                this.ArrayEntries[addressIndex] = null;
                            }
                        } // addressIndex
                    }
                } else {
                    if(newArraySize < this.ArrayEntries.Count) {
                        for(int addressIndex = newArraySize; addressIndex < this.ArrayEntries.Count; addressIndex++) {
                            if(this.ArrayEntries[addressIndex] != null) {
                                this.ArrayEntries[addressIndex].DestorySafe(variables);
                                this.ArrayEntries[addressIndex] = null;
                            }
                        } // addressIndex
                    }
                }

                // Set the new array size.
                if(newArraySize == 0) {
                    this.ArrayEntries.Clear();
                } else {
                    if(keepContents) {
                        // Add new entries
                        if(newArraySize > this.ArrayEntries.Count) {
                            for(int addIndex = 0; addIndex < (newArraySize - this.ArrayEntries.Count); addIndex++) {
                                this.ArrayEntries.Add(variables.mMemoryTable.AddValue(defaultEntryValue));
                            } // dwArrayItem
                            // Remove entries
                        } else if(newArraySize < this.ArrayEntries.Count) {
                            for(int addressIndex = (this.ArrayEntries.Count - 1); addressIndex >= newArraySize; addressIndex--) {
                                this.ArrayEntries.RemoveAt(addressIndex);
                            } //addressIndex
                        }
                    } else {
                        for(int addressIndex = 0; addressIndex < newArraySize; addressIndex++) {
                            this.ArrayEntries.Add(variables.mMemoryTable.AddValue(defaultEntryValue));
                        } //addressIndex
                    }
                }

                // return successful.
                return true;
            } //ResizeArraySafe function

            public bool DestorySafe(Variables variables, bool ignoreSafety = false)
            {
                if(!this.IsUsed) return false;

                // Make sure the entry isn't being used elsewhere, if it is then ... exit this procedure.
                if(!ignoreSafety && variables.FindByMemoryEntry(this) != null) return false;

                this.IsUsed = false;
                this.Value = "";

                if(this.ArrayEntries.Count > 0) {
                    for(int addressIndex = 0; addressIndex < this.ArrayEntries.Count; addressIndex++) {
                        if(!ignoreSafety) this.ArrayEntries[addressIndex].DestorySafe(variables);
                    } // addressIndex

                    this.ArrayEntries.Clear();
                }

                return true;
            } //DestorySafe

            public int GetArrayCount()
            {
                if(!this.IsUsed) return 0;
                return this.ArrayEntries.Count;
            } //GetArrayCount

            public MemoryEntry GetArrayPointer(int arrayIndex)
            {
                if(arrayIndex == 0) {
                    return this;
                } else if(arrayIndex > 0) {
                    if(this.ArrayEntries.Count != 0) {
                        if(arrayIndex > this.ArrayEntries.Count) return null;
                        return this.ArrayEntries[arrayIndex - 1];
                    }
                }

                return null;
            } //GetArrayPointer

            public MemoryEntry GetArrayPointer(int arrayIndex, MemoryEntry defaultPointer)
            {
                if(!this.IsUsed || arrayIndex < 1 || arrayIndex > this.ArrayEntries.Count) return defaultPointer;
                return this.ArrayEntries[arrayIndex - 1];
            } //GetArrayPointer

            public void SetArrayPointer(int arrayIndex, MemoryEntry newPointer)
            {
                if(!this.IsUsed) return;
                this.ArrayEntries[arrayIndex - 1] = newPointer;
            } //SetArrayPointer
        } //MemoryEntry class

        public static class Expression
        {
            private static System.Random mRandom = new System.Random();

            // private structure members
            private class SyntaxResults
            {
                public string OldSyntax = "";
                public string NewSyntax = "";
                public int    PosStart;
                public int    PosLen;
            } //SyntaxResults

            private class ParseFunctionResults
            {
                public bool   IsValid;
                public int    PosStart;
                public int    PosLen;
                public string FunctionName = "";
                public List<string> FunctionArguments = new List<string>();
            } //ParseFunctionResults

            private class ParseSpecialCharacterResults
            {
                public bool IsValid;
                public int  PosStart;
                public int  PosLen;
                public int  Character;
            } //ParseSpecialCharacterResults

            private class ParseVariableResults
            {
                public bool   IsValid;
                public int    PosStart;
                public int    PosLen;
                public string VarName  = "";
                public string VarIndex = "";
            } //ParseVariableResults

            private class ParseOperatorResults
            {
                public bool   IsValid;
                public int    PosStart;
                public int    PosLen;
                public string OperatorName = "";
                public string LeftOf       = "";
                public string RightOf      = "";
            } //ParseOperatorResults

            public static string Run(string expression, string locationID = "", string publicLocationID = "")
            {
                // Determine the syntax character.
                string strSyntaxChar = "&";

                // Store the expression into a temporary variable.
                string newExp = expression;

                int dwPos = 1;

                // Start a loop ...
                int syntaxPos = 0;
                SyntaxResults dSyntax;
                do {
                    // ... Find the first syntax character in the expression ...
                    syntaxPos = newExp.IndexOf(strSyntaxChar, dwPos - 1);
                    if(syntaxPos < 0) break;

                    // ... if(this is just a normal character and not a syntax character then ...
                    if(string.Equals(newExp.Substring(syntaxPos + strSyntaxChar.Length, 1), " ")) {
                        // ... Increase the position after the syntax character.
                        dwPos = (1 + syntaxPos + strSyntaxChar.Length);
                    } else {
                        // ... Parse the syntax expression ...
                        dSyntax = ParseSyntax(newExp, 1 + syntaxPos, locationID, publicLocationID);

                        // ... if(the new syntax is the same as the old then ...
                        if(string.Equals(dSyntax.NewSyntax, dSyntax.OldSyntax)) {
                            // ... Start after the syntax.
                            dwPos = (1 + syntaxPos + dSyntax.OldSyntax.Length);
                        } else {
                            // ... Replace the old syntax with the new syntax.
                            newExp = newExp.Replace(dSyntax.OldSyntax, dSyntax.NewSyntax);
                        }
                    }
                } while(true);

                // return the results.
                return newExp.Trim();
            } //Run

            private static SyntaxResults ParseSyntax(string expression, int syntaxStart, string locationID, string publicLocationID = "")
            {
                // Store the original expression.
                string strRet = expression;

                // Store some default results if this method fails.
                SyntaxResults results = new SyntaxResults();
                results.OldSyntax = expression;
                results.NewSyntax = expression;
                results.PosStart  = syntaxStart;
                results.PosLen    = expression.Length;

                // Check if this syntax is an expression.
                ParseFunctionResults dIsFunction = ParseFunctionString(expression, syntaxStart);

                // if(this is a function then ...
                if(dIsFunction.IsValid) {
                    bool   bFuncNoResults = false;
                    string strFuncRet     = ParseSyntaxFunction(dIsFunction, locationID, out bFuncNoResults, publicLocationID);

                    if(!bFuncNoResults) strRet = strFuncRet;
                    results.PosStart = dIsFunction.PosStart;
                    results.PosLen   = dIsFunction.PosLen;
                    // if(this isn't a function then it must be a operator ...
                } else {
                    //MsgBox "else start"
                    //DSSL_Runtime_DoExpression_ParseOperatorString

                    // ... Check if the syntax is a variable ...
                    ParseVariableResults dIsVar = ParseVariableString(expression, syntaxStart);

                    // ... if(this is a variable then ...
                    if(dIsVar.IsValid) {
                        results.PosStart = dIsVar.PosStart;
                        results.PosLen   = dIsVar.PosLen;

                        strRet = Globals.MyEnvironment.Variables.GetValue(dIsVar.VarName, locationID, "", publicLocationID, System.Convert.ToInt32(DSSL.Runtime.Expression.Run(dIsVar.VarIndex, locationID, publicLocationID)));
                        // ... if(this is not a variable then ...
                    } else {
                        // ... Check if the syntax is a special character ...
                        ParseSpecialCharacterResults dIsChar = ParseSpecialChracterString(expression, syntaxStart);

                        // ... if(this is a special character then ...
                        if(dIsChar.IsValid) {
                            results.PosStart = dIsChar.PosStart;
                            results.PosLen   = dIsChar.PosLen;
                            
                            strRet = System.Convert.ToChar(dIsChar.Character).ToString();
                            // ... if(this is not a special character then ...
                        } else {
                            // ... Check if the syntax is a variable ...
                            ParseOperatorResults dIsOP = ParseOperatorString(expression, syntaxStart);

                            // ... if(this is a variable then ...
                            if(dIsOP.IsValid) {
                                string strLeftOf  = Run(dIsOP.LeftOf, locationID, publicLocationID);
                                string strRightOf = Run(dIsOP.RightOf, locationID, publicLocationID);
                                bool bOpNoResults = false;
                                bool opValid      = false;
                                string strOpRet   = ParseSyntaxOperator(dIsOP.OperatorName, strLeftOf, strRightOf, out bOpNoResults, out opValid);
                                if(!bOpNoResults) strRet = strOpRet;
                            }

                            results.PosStart = dIsOP.PosStart;
                            results.PosLen   = dIsOP.PosLen;
                        }
                    }
                }

                results.NewSyntax = strRet;
                if(results.PosLen == 0) {
                    results.OldSyntax = expression;
                } else {
                    results.OldSyntax = expression.Substring(results.PosStart - 1, results.PosLen);
                }
                return results;
            } //ParseSyntax

            // This procedure will test if an syntax given in an expression is a function.
            //
            // Rules:
            //   * All syntaxes begin with '&'.
            //   * All functions start with a name followed by a parenthesis start and end,
            //     what is between the parenthesis is the function's arguments.
            private static ParseFunctionResults ParseFunctionString(string expression, int startAt)
            {
                // Find where the syntax is in the outside part of the expression.
                int dwSyntaxPos = StringManager.FindStringOutsideRev(expression, startAt, "&", 1);
                // if(the syntax character was not found then ... exit this procedure.
                if(dwSyntaxPos == 0) return new ParseFunctionResults();

                // Find the first parenthesis start character.
                int dwParaStart = StringManager.FindStringOutside(expression, dwSyntaxPos, "(");
                // if(no parenthesis start character was found then ... exit this procedure.
                if(dwParaStart == 0) return new ParseFunctionResults();

                // Find if there is any spaces after the syntax and before the parenthesis start character.
                int dwFirstSpace = StringManager.FindStringOutside(expression, dwSyntaxPos, " ", dwParaStart);
                // if(a space was found then ... exit this procedure.
                if(dwFirstSpace != 0) return new ParseFunctionResults();

                // Find the first parenthesis end character.
                int dwParaEnd = StringManager.FindStringOutside(expression, dwParaStart + 1, ")");
                // ... if(the first parenthesis end character was not found then ... exit this procedure.
                if(dwParaEnd == 0) return new ParseFunctionResults();

                // Parse the information.
                ParseFunctionResults results = new ParseFunctionResults();
                results.IsValid      = true;
                results.PosStart     = dwSyntaxPos;
                results.PosLen       = ((dwParaEnd + 1) - dwSyntaxPos);
                results.FunctionName = expression.Substring(dwSyntaxPos, dwParaStart - (dwSyntaxPos + 1));

                // Extract the arguments.
                string strArgs = expression.Substring(dwParaStart, dwParaEnd - (dwParaStart + 1));

                // if(any arguments was extracted then ...
                if(!string.IsNullOrEmpty(strArgs)) {
                    // ... Split the arguments up into an array ...
                    List<string> argumentsList; StringManager.SmartSplit(strArgs, ",", out argumentsList);
                    // ... Go through each argument...
                    for(int argumentIndex = 0; argumentIndex < argumentsList.Count; argumentIndex++) {
                        // ... Store the argument into the arguments array ...
                        results.FunctionArguments.Add(argumentsList[argumentIndex]);
                        // ... Go to the next argument.
                    } // dwArg
                }

                // return the results.
                return results;
            } //ParseFunctionString

            private static string ParseSyntaxFunction(ParseFunctionResults functionDesc, string locationID, out bool outNoResults, string publicLocationID = "")
            {
                // By default return nothing.
                outNoResults = false;

                List<string> arguments = new List<string>();
                if(functionDesc.FunctionArguments.Count == 0) {
                    arguments.Clear();
                } else {
                    for(int argumentIndex = 0; argumentIndex < functionDesc.FunctionArguments.Count; argumentIndex++) {
                        arguments.Add(functionDesc.FunctionArguments[argumentIndex]);
                        arguments[arguments.Count - 1] = arguments[arguments.Count - 1].Trim();
                        //MsgBox("Before: " + arguments.Count + ": " + arguments[arguments.Count - 1]);
                        arguments[arguments.Count - 1] = StringManager.FixSpecialStrings(StringManager.GetQuoteString(Expression.Run(arguments[arguments.Count - 1], locationID, publicLocationID)));
                        //MsgBox("After: " + arguments.Count + ": " + arguments[arguments.Count - 1]);
                    } // dwArgIndex
                }

                string strVarName, strVarName2;
                string[] strVarIndex = new string[1];
                int variableIndex;
                string strAddLocationID;
                bool bAnyFound;
                switch(functionDesc.FunctionName.ToLower()) {
                    // General Functions:
                    case("time"):
                        if(arguments.Count == 0) {
                            return System.Environment.TickCount.ToString();
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                        // Variable Functions:
                    case("add"):
                        strVarName = arguments[0];
                        Parser.Syntax.ParseArrayNameData(strVarName, out strVarName, out strVarIndex[0]);

                        if(arguments.Count == 1) {
                            Globals.MyEnvironment.Variables.SetValue(strVarName, "", System.Convert.ToInt32(strVarIndex[0]), locationID, publicLocationID);
                            return "";
                        } else if(arguments.Count == 2) {
                            Globals.MyEnvironment.Variables.SetValue(strVarName, arguments[1], System.Convert.ToInt32(strVarIndex[0]), locationID, publicLocationID);
                            return "";
                        } else if(arguments.Count == 3) {
                            Parser.VariableScopes eScope = Parser.Translator.VariableScopeToEnum(arguments[2]);

                            Variables.LocationTypes locationType;
                            string parentLocation;
                            strAddLocationID = Runtime.Variables.DetermineLocationID(Runtime.Variables.ScopeToLocationType(eScope), locationID, publicLocationID, out locationType, out parentLocation);

                            Globals.MyEnvironment.Variables.SetValue(strVarName, arguments[1], System.Convert.ToInt32(strVarIndex[0]), strAddLocationID, publicLocationID);
                            return "";
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("get"):
                        strVarName = arguments[0];
                        Parser.Syntax.ParseArrayNameData(strVarName, out strVarName, out strVarIndex[0]);

                        if(arguments.Count == 1) {
                            return Globals.MyEnvironment.Variables.GetValue(strVarName, locationID, "", publicLocationID, System.Convert.ToInt32(strVarIndex[0]));
                        } else if(arguments.Count == 2) {
                            return Globals.MyEnvironment.Variables.GetValue(strVarName, locationID, arguments[1], publicLocationID, System.Convert.ToInt32(strVarIndex[0]));
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("del"):
                        strVarName = arguments[0];
                        Parser.Syntax.ParseArrayNameData(strVarName, out strVarName, out strVarIndex[0]);

                        if(arguments.Count == 1) {
                            Globals.MyEnvironment.Variables.Remove(strVarName, locationID, true, publicLocationID);
                            return "";
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("set"):
                        if(arguments.Count == 2) {
                            strVarName = arguments[0];
                            Parser.Syntax.ParseArrayNameData(strVarName, out strVarName, out strVarIndex[0]);

                            Globals.MyEnvironment.Variables.SetValue(strVarName, arguments[1], System.Convert.ToInt32(strVarIndex[0]), locationID, publicLocationID);
                            return "";
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("link"):
                        if(arguments.Count == 2) {
                            strVarName = arguments[0];
                            Parser.Syntax.ParseArrayNameData(strVarName, out strVarName, out strVarIndex[0]);

                            strVarName2 = arguments[1];
                            Parser.Syntax.ParseArrayNameData(strVarName2, out strVarName2, out strVarIndex[1]);

                            Globals.MyEnvironment.Variables.Link(strVarName, System.Convert.ToInt32(strVarIndex[0]), strVarName2, locationID, System.Convert.ToInt32(strVarIndex[1]), locationID, publicLocationID);
                            return "";
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("unlink"):
                        strVarName = arguments[0];
                        Parser.Syntax.ParseArrayNameData(strVarName, out strVarName, out strVarIndex[0]);

                        if(arguments.Count == 1) {
                            Globals.MyEnvironment.Variables.Unlink(strVarName, locationID, publicLocationID);
                            return "";
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("getsize"):
                        strVarName = arguments[0];
                        Parser.Syntax.ParseArrayNameData(strVarName, out strVarName, out strVarIndex[0]);

                        if(arguments.Count == 1) {
                            variableIndex = Globals.MyEnvironment.Variables.FindIndex(strVarName, locationID, publicLocationID);

                            if(variableIndex >= 0) {
                                return Globals.MyEnvironment.Variables.GetArraySize(1 + variableIndex).ToString();
                            } else {
                                return "-1";
                            }
                        } else {
                            return "-1"; //ERR_INVALIDARGSCOUNT
                        }
                    case("allocate"):
                        if(arguments.Count == 2) {
                            variableIndex = Globals.MyEnvironment.Variables.FindIndex(arguments[0], locationID, publicLocationID);
                            Globals.MyEnvironment.Variables.SetArraySize(1 + variableIndex, System.Convert.ToInt32(arguments[1]));
                            return "";
                        } else if(arguments.Count == 3) {
                            variableIndex = Globals.MyEnvironment.Variables.FindIndex(arguments[0], locationID, publicLocationID);
                            Globals.MyEnvironment.Variables.SetArraySize(1 + variableIndex, System.Convert.ToInt32(arguments[1]), System.Convert.ToBoolean(arguments[2]));
                            return "";
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                        // String Functions:
                    case("len"):
                        if(arguments.Count == 1) {
                            return arguments[0].Length.ToString();
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("mid"):
                        if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return arguments[0].Substring(System.Convert.ToInt32(arguments[1]) - 1);
                            }
                        } else if(arguments.Count == 3) {
                            if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else if(!Tools.IsNumeric(arguments[2])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "3");
                            } else {
                                return arguments[0].Substring(System.Convert.ToInt32(arguments[1]) - 1, System.Convert.ToInt32(arguments[2]));
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("abbr"): return SyntaxFunctions.ABBR(arguments).ToString();
                        // Math Functions:
                    case("seed"):
                        if(arguments.Count == 0) {
                            mRandom = new System.Random(System.Environment.TickCount);
                            return "";
                        } else if(arguments.Count == 1) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else {
                                mRandom = new System.Random(System.Convert.ToInt32(arguments[0]));
                                return "";
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("rnd"):
                        if(arguments.Count == 0) {
                            return mRandom.NextDouble().ToString();
                        } else if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return (System.Convert.ToInt32(arguments[0]) + ((System.Convert.ToInt32(arguments[1]) - System.Convert.ToInt32(arguments[0])) * mRandom.NextDouble())).ToString();
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("round"):
                        if(arguments.Count == 1) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else {
                                return System.Math.Round(System.Convert.ToDouble(arguments[0]), 0).ToString();
                            }
                        } else if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return System.Math.Round(System.Convert.ToDouble(arguments[0]), System.Convert.ToInt32(arguments[1])).ToString();
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("addition"): //was also called add() as earlier variable add() function, scripts might needs fixed.
                        if(arguments.Count == 2) {
                            if(Tools.IsNumeric(arguments[0]) && Tools.IsNumeric(arguments[1])) {
                                return (System.Convert.ToInt32(arguments[0]) + System.Convert.ToInt32(arguments[1])).ToString();
                            } else {
                                return arguments[0] + arguments[1];
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("sub"):
                        if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return (System.Convert.ToInt32(arguments[0]) - System.Convert.ToInt32(arguments[1])).ToString();
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("mul"):
                        if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return (System.Convert.ToInt32(arguments[0]) * System.Convert.ToInt32(arguments[1])).ToString();
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("div"):
                        if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return (System.Convert.ToInt32(arguments[0]) / System.Convert.ToInt32(arguments[1])).ToString();
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("div2"):
                        if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return System.Convert.ToInt32(System.Convert.ToInt32(arguments[0]) / System.Convert.ToInt32(arguments[1])).ToString();
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("mod"):
                        if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return (System.Convert.ToInt32(arguments[0]) % System.Convert.ToInt32(arguments[1])).ToString();
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("pwr"):
                        if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return System.Math.Pow(System.Convert.ToInt32(arguments[0]), System.Convert.ToInt32(arguments[1])).ToString();
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("sqrt"): return SyntaxFunctions.SquareRoot(arguments).ToString();
                    case("per"): return SyntaxFunctions.Percentage(arguments).ToString();
                        // Comparison Functions:
                    case("is"):
                        if(arguments.Count == 2) {
                            return (arguments[0] == arguments[1] ? "1" : "0");
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("less"):
                        if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return (System.Convert.ToInt32(arguments[0]) < System.Convert.ToInt32(arguments[1]) ? "1" : "0");
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("more"):
                        if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return (System.Convert.ToInt32(arguments[0]) > System.Convert.ToInt32(arguments[1]) ? "1" : "0");
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("isorless"):
                        if(arguments.Count == 2) {
                            if(!Tools.IsNumeric(arguments[0])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                            } else if(!Tools.IsNumeric(arguments[1])) {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            } else {
                                return (System.Convert.ToInt32(arguments[0]) <= System.Convert.ToInt32(arguments[1]) ? "1" : "0");
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSCOUNT;
                        }
                    case("isormore"): return SyntaxFunctions.IsOrMore(arguments).ToString();
                    case("if"): return SyntaxFunctions.IFStatement(arguments).ToString();
                    case("call"):
                        List<string> functionResults = SyntaxFunctions.CallFunction(Globals.MyEnvironment.Scripts[publicLocationID], arguments);
                        if(functionResults != null && functionResults.Count > 0) {
                            return functionResults[0];
                        } else {
                            return "";
                        }
                    default:
                        // File Helper Functions
                        if(functionDesc.FunctionName.StartsWith("file.", System.StringComparison.CurrentCultureIgnoreCase)) {
                            return SyntaxFunctions.FileCommand(functionDesc.FunctionName, arguments).ToString();
                        } else {
                            if(!Settings.DontAllowExternalExpressions && Globals.MyHelper != null) {
                                //return DSSL_External_DoExpressionFunction(functionDesc.functionName, dwArgsCount, strArgsItem, bAnyFound);
                                string results = Globals.MyHelper.DoExpressionFunction(functionDesc.FunctionName, arguments, out bAnyFound);
                                if(!bAnyFound) {
                                    return Errors.ERR_CMDNOTFOUND;
                                } else {
                                    return results;
                                }
                            } else {
                                outNoResults = true;
                                return Errors.ERR_CMDNOTFOUND;
                            }
                        }
                }
            } //ParseSyntaxFunction

            private static string ParseSyntaxOperator(string Operator, string leftOf, string rightOf, out bool outNoResults, out bool outValid)
            {
                // By default return nothing.
                outNoResults = false;
                outValid     = true;

                // Fix the left and right syntax strings.
                leftOf = StringManager.FixSpecialStrings(leftOf);
                rightOf = StringManager.FixSpecialStrings(rightOf);

                bool bAnyFound = false;
                bool bIsLeft = false, bIsRight = false;
                switch(Operator.ToLower()) {
                    case("+"):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) + System.Convert.ToInt32(rightOf)).ToString();
                        } else {
                            return leftOf + rightOf;
                        }
                    case("-"):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) - System.Convert.ToInt32(rightOf)).ToString();
                        } else {
                            return "";
                        }
                    case("*"):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) * System.Convert.ToInt32(rightOf)).ToString();
                        } else {
                            return "";
                        }
                    case("/"):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            if(System.Convert.ToInt32(rightOf) == 0) {
                                return (0).ToString();
                            } else {
                                return (System.Convert.ToInt32(leftOf) / System.Convert.ToInt32(rightOf)).ToString();
                            }
                        } else {
                            return "";
                        }
                    case("\\"):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) / System.Convert.ToInt32(rightOf)).ToString();
                        } else {
                            return "";
                        }
                    case("mod"):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) % System.Convert.ToInt32(rightOf)).ToString();
                        } else {
                            return "";
                        }
                    case("pwr"):
                    case("pow"):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return System.Math.Pow(System.Convert.ToInt32(leftOf), System.Convert.ToInt32(rightOf)).ToString();
                        } else {
                            return "";
                        }
                    case("="):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) == System.Convert.ToInt32(rightOf) ? "1" : "0");
                        } else {
                            return (leftOf == rightOf ? "1" : "0");
                        }
                    case("!="):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) == System.Convert.ToInt32(rightOf) ? "0" : "1");
                        } else {
                            return (leftOf == rightOf ? "0" : "1");
                        }
                    case("<"):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) < System.Convert.ToInt32(rightOf) ? "1" : "0");
                        } else {
                            return "0"; //return "";
                        }
                    case(">"):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) > System.Convert.ToInt32(rightOf) ? "1" : "0");
                        } else {
                            return "0"; //return "";
                        }
                    case("<="):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) <= System.Convert.ToInt32(rightOf) ? "1" : "0");
                        } else {
                            return "0"; //return "";
                        }
                    case(">="):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) >= System.Convert.ToInt32(rightOf) ? "1" : "0");
                        } else {
                            return "0"; //return "";
                        }
                    case("%"):
                        if(Tools.IsNumeric(leftOf) && Tools.IsNumeric(rightOf)) {
                            return (System.Convert.ToInt32(leftOf) * (System.Convert.ToInt32(rightOf) / 100)).ToString();
                        } else {
                            return "0"; //return "";
                        }
                    case("and"): //Compares if the string to the left is true and the string to the right is true.
                        if(Tools.IsNumeric(leftOf)) {
                            bIsLeft = (System.Convert.ToInt32(leftOf) != 0);
                        } else {
                            bIsLeft = !string.IsNullOrEmpty(leftOf);
                        }
                        if(Tools.IsNumeric(rightOf)) {
                            bIsRight = (System.Convert.ToInt32(rightOf) != 0);
                        } else {
                            bIsRight = !string.IsNullOrEmpty(rightOf);
                        }
                        return ((bIsLeft && bIsRight) ? "1" : "0");
                    case("or"): //Compares to if either the left or right strings is true.
                        if(Tools.IsNumeric(leftOf)) {
                            bIsLeft = (System.Convert.ToInt32(leftOf) != 0);
                        } else {
                            bIsLeft = !string.IsNullOrEmpty(leftOf);
                        }
                        if(Tools.IsNumeric(rightOf)) {
                            bIsRight = (System.Convert.ToInt32(rightOf) != 0);
                        } else {
                            bIsRight = !string.IsNullOrEmpty(rightOf);
                        }
                        return ((bIsLeft || bIsRight) ? "1" : "0");
                    default:
                        outValid = false;
                        if(!Settings.DontAllowExternalExpressions && Globals.MyHelper != null) {
                            string results = Globals.MyHelper.DoOperator(Operator, leftOf, rightOf, out bAnyFound);
                            if(!bAnyFound) {
                                return "";
                            } else {
                                return results;
                            }
                        } else {
                            outNoResults = true;
                            return "";
                        }
                }
            } //ParseSyntaxOperator

            // This procedure will test if an syntax given in an expression is a function.
            //
            // Rules:
            //   * All syntaxes begin with '&'.
            //   * All variables expressions begin with a name, and nothing must follow after it.
            //     What all indicates the end of a variable is either if it hits the end of the
            //     string or it's a non-standard character. (a-z, A-Z, 0-9)
            //   * All variables must not be an operator.
            private static ParseVariableResults ParseVariableString(string expression, int startAt)
            {
                // Find where the syntax is in the outside part of the expression.
                int dwSyntaxPos = StringManager.FindStringOutsideRev(expression, startAt, SyntaxVariableOperator, 1);
                // if(the syntax character was not found then ... exit this procedure.
                if(dwSyntaxPos == 0) return new ParseVariableResults();

                // Find the end of the variable's name ...
                int dwEndPos = Parser.Syntax.FindEndOfSyntax(expression, dwSyntaxPos + SyntaxVariableOperator.Length, true, true, true, true, true, true);

                // Parse the information.
                ParseVariableResults dRet = new ParseVariableResults();
                dRet.IsValid = true;
                dRet.PosStart = dwSyntaxPos;
                dRet.PosLen = (dwEndPos - dwSyntaxPos);
                string strName = expression.Substring((dwSyntaxPos - 1) + SyntaxVariableOperator.Length, dwEndPos - (dwSyntaxPos + SyntaxVariableOperator.Length));
                Parser.Syntax.ParseArrayNameData(strName, out dRet.VarName, out dRet.VarIndex);

                // if(the variable name is an operator then ... exit this procedure.
                if(Parser.Syntax.IsOperatorName(dRet.VarName)) return new ParseVariableResults();

                // return the results.
                return dRet;
            } //ParseVariableString

            // This procedure will test if an syntax given in an expression is a function.
            //
            // Rules:
            //   * All syntaxes begin with '&'.
            //   * All variables expressions begin with a name, and nothing must follow after it.
            //     What all indicates the end of a variable is either if it hits the end of the
            //     string or it's a non-standard character. (a-z, A-Z, 0-9)
            //   * All variables must not be an operator.
            private static ParseSpecialCharacterResults ParseSpecialChracterString(string expression, int startAt)
            {
                // Find where the syntax is in the outside part of the expression.
                int dwSyntaxPos = StringManager.FindStringOutsideRev(expression, startAt, SyntaxSpecialOperator, 1);
                // if(the syntax character was not found then ... exit this procedure.
                if(dwSyntaxPos == 0) return new ParseSpecialCharacterResults();

                // Find the end of the variable's name ...
                int dwEndPos = Parser.Syntax.FindEndOfSyntax(expression, dwSyntaxPos + SyntaxSpecialOperator.Length, false, false, true, false, false, false);

                // Parse the information.
                ParseSpecialCharacterResults dRet = new ParseSpecialCharacterResults();
                dRet.IsValid = true;
                dRet.PosStart = dwSyntaxPos;
                dRet.PosLen = (dwEndPos - dwSyntaxPos);
                dRet.Character = System.Convert.ToInt32(expression.Substring((dwSyntaxPos - 1) + SyntaxVariableOperator.Length, dwEndPos - (dwSyntaxPos + SyntaxVariableOperator.Length)));

                // return the results.
                return dRet;
            } //ParseSpecialChracterString

            // This procedure will test if an syntax given in an expression is a function.
            //
            // Rules:
            //   * All syntaxes begin with '&'.
            //   * All operator expressions begin with a name.
            private static ParseOperatorResults ParseOperatorString(string expression, int startAt)
            {
                // Find where the syntax is in the outside part of the expression.
                int dwSyntaxPos = StringManager.FindStringOutsideRev(expression, startAt, "&", 1);
                // if(the syntax character was not found then ... exit this procedure.
                if(dwSyntaxPos == 0) return new ParseOperatorResults();

                // Find the end of the variable's name ...
                int dwEndPos = Parser.Syntax.FindEndOfOperator(expression, dwSyntaxPos + 1);
                if(dwEndPos == 0) return new ParseOperatorResults();

                // Parse the information.
                ParseOperatorResults dRet = new ParseOperatorResults();
                dRet.IsValid = true;
                dRet.OperatorName = expression.Substring(dwSyntaxPos, dwEndPos - (dwSyntaxPos + "&".Length)).Trim();

                int dwParenLeft = 0, dwParenRight = 0;
                if(StringManager.IsInsideParenthesis(expression, dwSyntaxPos, out dwParenLeft, out dwParenRight)) {
                    dRet.PosStart = dwParenLeft;
                    dRet.PosLen = dwParenRight - (dwParenLeft - "(".Length);
                    dRet.LeftOf = expression.Substring((dwParenLeft - 1) + "(".Length, dwSyntaxPos - (dwParenLeft + "(".Length)).Trim();
                    dRet.RightOf = expression.Substring((dwSyntaxPos - 1) + ("&" + dRet.OperatorName).Length, dwParenRight - (dwSyntaxPos + ("&" + dRet.OperatorName).Length)).Trim();
                } else {
                    dRet.PosStart = 1;
                    dRet.PosLen = expression.Length;
                    dRet.LeftOf = expression.Substring(0, dwSyntaxPos - 1).Trim();
                    dRet.RightOf = expression.Substring((dwSyntaxPos - 1) + ("&" + dRet.OperatorName).Length).Trim();
                }

                // return the results.
                return dRet;
            } //ParseOperatorString

            private static class SyntaxFunctions
            {
                // Needs to convert stuff like 'This is a test" to "TIAT".
                public static string ABBR(List<string> arguments)
                {
                    if (arguments == null || arguments.Count == 0) return Errors.ERR_INVALIDARGSCOUNT;

                    if(arguments.Count == 1) {
                        string[] strWords = arguments[0].Split(' ');
                        if(strWords == null || strWords.Length == 0) return "";

                        string strRet = "";
                        string newWord = "";
                        foreach(string word in strWords) {
                            newWord = word.Trim();
                            if(!string.IsNullOrEmpty(newWord)) {
                                if(!Tools.IsNumeric(newWord)) {
                                    strRet += newWord.Substring(0, 1).ToUpper();
                                } else {
                                    strRet = (string.IsNullOrEmpty(strRet) ? "" : strRet + " ") + word;
                                }
                            }
                        } // dwWord
                        return strRet;
                    } else if(arguments.Count == 2) {
                        if(Tools.IsNumeric(arguments[1])) {
                            string[] strWords = arguments[0].Split(' ');
                            if(strWords == null || strWords.Length == 0) return "";

                            string strRet = "";
                            string newWord = "";
                            foreach(string word in strWords) {
                                newWord = word.Trim();
                                if(!string.IsNullOrEmpty(newWord)) {
                                    if(newWord.Length < System.Convert.ToInt32(arguments[1]) && !Tools.IsNumeric(newWord)) {
                                        strRet += newWord.Substring(0, 1).ToUpper();
                                    } else {
                                        strRet = (string.IsNullOrEmpty(strRet) ? "" : strRet + " ") + newWord;
                                    }
                                }
                            } // dwWord
                            return strRet;
                        } else {
                            return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                        }
                    } else {
                        return Errors.ERR_INVALIDARGSCOUNT;
                    }
                } //ABBR

                public static string SquareRoot(List<string> arguments)
                {
                    if(arguments == null || arguments.Count == 0) return Errors.ERR_INVALIDARGSCOUNT;

                    if(arguments.Count == 1) {
                        if(Tools.IsNumeric(arguments[0])) {
                            return System.Math.Sqrt(System.Convert.ToInt32(arguments[0])).ToString();
                        } else {
                            return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                        }
                    } else {
                        return Errors.ERR_INVALIDARGSCOUNT;
                    }
                } //SquareRoot

                //per(value,percentage)
                public static string Percentage(List<string> arguments)
                {
                    if(arguments == null || arguments.Count == 0) return Errors.ERR_INVALIDARGSCOUNT;

                    if(arguments.Count == 2) {
                        if(Tools.IsNumeric(arguments[0])) {
                            if(Tools.IsNumeric(arguments[1])) {
                                return (System.Convert.ToInt32(arguments[0]) * (System.Convert.ToInt32(arguments[1]) / 100)).ToString();
                            } else {
                                return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                            }
                        } else {
                            return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                        }
                    } else {
                        return Errors.ERR_INVALIDARGSCOUNT;
                    }
                } //Percentage

                // Uses Visual Basic's built-in functions for file manipulating within the scripting functions.
                public static string FileCommand(string functionName, List<string> arguments)
                {
                    if(arguments == null || arguments.Count == 0) return Errors.ERR_INVALIDARGSCOUNT;

                    if(Settings.DontAllowFileOperations) return Errors.ERR_FILENOTALLOWED;
                    if(!functionName.StartsWith("file.", System.StringComparison.CurrentCultureIgnoreCase)) return "";

                    // TODO: Add some other framework for file support.
                    //Dim hFile As int
                    switch(functionName.Substring("file".Length)) {
                        //Case "load" 'File.Load(file_name, [open_type]), Returns file_handle
                        //    if(ArgsCount = 1) {
                        //        hFile = FreeFile()
                        //        FileOpen(hFile, ArgItem(0), OpenMode.Binary)
                        //    } else if(ArgsCount = 2) {
                        //        hFile = FreeFile()
                        //        switch(ArgItem(1).ToLower()) {
                        //            case("binary" : FileOpen(hFile, ArgItem(0), OpenMode.Binary)
                        //            case("input" : FileOpen(hFile, ArgItem(0), OpenMode.Input)
                        //            case("output" : FileOpen(hFile, ArgItem(0), OpenMode.Output)
                        //        }
                        //    } else {
                        //        strRet = Errors.ERR_INVALIDARGSCOUNT
                        //    }
                        //Case "close" 'File.Close(file_handle)
                        //    if(ArgsCount = 1) {
                        //        hFile = System.Convert.ToInt32(ArgItem(0))
                        //        if(hFile >= 1) FileClose(hFile)
                        //    } else {
                        //        strRet = Errors.ERR_INVALIDARGSCOUNT
                        //    }
                        //    // Case "save"  'File.Save(file_name)
                        //    //    if(dwArgsCount = 1) {
                        //    //    } else {
                        //    //        strRet = ERR_INVALIDARGSCOUNT
                        //    //    }
                        //Case "getbyte" 'File.GetByte(file_handle, [file_position]), Returns file_byte
                        //    if(ArgsCount = 1) {
                        //        hFile = System.Convert.ToInt32(ArgItem(0))
                        //        if(hFile >= 1) {
                        //            Dim aData As Byte
                        //            FileGet(hFile, aData)
                        //            strRet = CStr(aData)
                        //        }
                        //    } else if(ArgsCount = 2) {
                        //        hFile = System.Convert.ToInt32(ArgItem(0))
                        //        if(hFile >= 1) {
                        //            Dim aData As Byte
                        //            FileGet(hFile, aData, CLng(System.Convert.ToInt32(ArgItem(1))))
                        //            strRet = CStr(aData)
                        //        }
                        //    } else {
                        //        strRet = Errors.ERR_INVALIDARGSCOUNT
                        //    }
                        //Case "putbyte" 'File.PutByte(file_handle, [file_position], byte)
                        //    if(ArgsCount = 3) {
                        //        hFile = System.Convert.ToInt32(ArgItem(0))
                        //        if(hFile >= 1) {
                        //            Dim aData As Byte
                        //            aData = CByte(System.Convert.ToInt32(ArgItem(2)))
                        //            if(Len(ArgItem(1)) = 0) {
                        //                FilePut(hFile, aData)
                        //            } else {
                        //                FilePut(hFile, aData, CLng(System.Convert.ToInt32(ArgItem(1))))
                        //            }
                        //        }
                        //    } else {
                        //        return Errors.ERR_INVALIDARGSCOUNT
                        //    }
                        default:
                            return Errors.ERR_CMDNOTFOUND;
                    }
                } //FileCommand

                // if(A) ... or .... if(A, B) ... or ... if(A, B, C)
                public static string IFStatement(List<string> arguments)
                {
                    if(arguments == null || arguments.Count == 0) return Errors.ERR_INVALIDARGSCOUNT;

                    if(arguments.Count == 1) {
                        if(Tools.IsNumeric(arguments[0])) {
                            if(System.Convert.ToInt32(arguments[0]) != 0) {
                                return arguments[0];
                            } else {
                                return "";
                            }
                        } else {
                            if(!string.IsNullOrEmpty(arguments[0])) {
                                return arguments[0];
                            } else {
                                return "";
                            }
                        }
                    } else if(arguments.Count == 2) {
                        if(Tools.IsNumeric(arguments[0])) {
                            if(System.Convert.ToInt32(arguments[0]) != 0) {
                                return arguments[0];
                            } else {
                                return arguments[1];
                            }
                        } else {
                            if(!string.IsNullOrEmpty(arguments[0])) {
                                return arguments[0];
                            } else {
                                return arguments[1];
                            }
                        }
                    } else if(arguments.Count == 3) {
                        if(Tools.IsNumeric(arguments[0])) {
                            if(System.Convert.ToInt32(arguments[0]) != 0) {
                                return arguments[1];
                            } else {
                                return arguments[2];
                            }
                        } else {
                            if(!string.IsNullOrEmpty(arguments[0])) {
                                return arguments[1];
                            } else {
                                return arguments[2];
                            }
                        }
                    } else {
                        return Errors.ERR_INVALIDARGSCOUNT;
                    }
                } //IFStatement

                public static List<string> CallFunction(Script homeScript, List<string> arguments)
                {
                    if(arguments != null || arguments.Count >= 1) {
                        Script script = null;
                        Script.ParsedFunction function = Globals.MyEnvironment.Scripts.FindFunction(arguments[0], homeScript, out script);

                        if(function != null) {
                            string[] newArgs = null;
                            if((arguments.Count - 1) > 0) {
                                newArgs = new string[arguments.Count - 1];
                                for(int argumentIndex = 0; argumentIndex < (arguments.Count - 1); argumentIndex++) {
                                    newArgs[argumentIndex] = arguments[1 + argumentIndex]; //GetQuoteString(DSSL_Runtime_GetEventParameterValue(dFunction.PropertyName(dwArg), Pointers, scripts, locationID, inHomeScript))
                                } //argumentIndex
                            }

                            // ... Run the function and return the results.
                            bool goodCall = false;
                            Instances cInstances = new Instances();
                            return cInstances.RunFunction(script, function, true, newArgs, out goodCall);
                        } else {
                            List<string> results = new List<string>();
                            results.Add(Errors.ERR_FUNCTIONNOTFOUND);
                            return results;
                        }
                    } else {
                        List<string> results = new List<string>();
                        results.Add(Errors.ERR_INVALIDARGSCOUNT);
                        return results;
                    }
                } //CallFunction

                public static string IsOrMore(List<string> arguments)
                {
                    if(arguments == null || arguments.Count == 0) return Errors.ERR_INVALIDARGSCOUNT;

                    if(arguments.Count == 2) {
                        if(!Tools.IsNumeric(arguments[0])) {
                            return Errors.ERR_INVALIDARGSDATA.Replace("%n", "1");
                        } else if(!Tools.IsNumeric(arguments[1])) {
                            return Errors.ERR_INVALIDARGSDATA.Replace("%n", "2");
                        } else {
                            return (System.Convert.ToInt32(arguments[0]) >= System.Convert.ToInt32(arguments[1]) ? "1" : "0");
                        }
                    } else {
                        return Errors.ERR_INVALIDARGSCOUNT;
                    }
                } //IsOrMore
            } //SyntaxFunctions
        } //Expression

        public class Instances
        {
            private List<Instance> mItem = new List<Instance>();

            // TODO: Rename and relocate
            private class Instance
            {
                public string InstanceID;
                public Script Script;
                public int    EventIndex;
                public bool   Finished; //Only gets set to 'True' when the instance is completed, this will cause the instance to be removed.
                public Script.ParsedFunction Function;

                public int SetCurEventIndex(int newIndex)
                {
                    int oldIndex = this.EventIndex;
                    this.EventIndex = newIndex;
                    return oldIndex;
                } //SetCurEventIndex function

                public int GetEventLinesCount()
                {
                    if(this.Script == null) return 0;
                    return this.Script.Desc.Events.Count;
                } //GetEventLinesCount function

                public Framework.Event GetEventDesc() { return GetEventDesc(this.EventIndex); }

                public Framework.Event GetEventDesc(int eventIndex)
                {
                    if(this.Script == null) return null;
                    return this.Script.Desc.Events[eventIndex - 1];
                } //GetEventDesc function

                public Framework.EventsCollection GetCurPointers()
                {
                    if(this.Script == null) return null;
                    return this.Script.Desc.Pointers;
                } //GetCurPointers function
            } //Instance

            private bool EndLastInstance()
            {
                // Get the event index.
                if(this.mItem.Count == 0) return false;

                // Check for any 'out of bounds', if so ... exit with error.
                Script script = this.mItem[this.mItem.Count - 1].Script;
                if(script == null) return false;

                // if this instance is a function then remove it's arguments from the private variables collection.
                Script.ParsedFunction function = this.mItem[this.mItem.Count - 1].Function;
                if(function != null) this.RemoveFunctionArgs(this.mItem.Count, function.Desc);

                // Remove the instance's private variables collection.
                Globals.MyEnvironment.Variables.RemoveAllByLocation(Runtime.Variables.LocationTypes.Locale, this.mItem.Count.ToString());

                // Remove the last instance.
                this.Pop();
                // ... if(no instances are left then ... exit this procedure (with an error).
                return (this.mItem.Count != 0);
            } //EndLastInstance

            public void DoStop() { this.Clear(); }

            public int Count { get { return this.mItem.Count; } }

            public void Clear() { this.mItem.Clear(); }

            public void Pop() { this.mItem.RemoveAt(this.mItem.Count - 1); }

            public int Push(Script script, int eventIndex)
            {
                Instance newItem = new Instance();
                newItem.Script     = script;
                newItem.EventIndex = eventIndex;
                newItem.Finished   = false;
                newItem.InstanceID = Variables.MakeLocationID(Variables.LocationTypes.Locale, (this.mItem.Count + 1).ToString());
                this.mItem.Add(newItem);

                return this.mItem.Count; //this.mItem[this.mItem.Count - 1];
            } //Push

            // All these does is add new variables based on a function's arguments with
            // the reference of the instance.
            public void AddFunctionArgs(int instanceIndex, Framework.Properties.FunctionStatement functionProps, string[] arguments, string previousLocationID = "", string publicLocation = "")
            {
                if(arguments == null || arguments.Length == 0) return;

                string localeLocationID = Runtime.Variables.MakeLocationID(Runtime.Variables.LocationTypes.Locale, instanceIndex.ToString());

                if(functionProps.Properties.Count > 0) {
                    string propValue = "", propName = "", varName = "";
                    for(int propertyIndex = 1; propertyIndex <= functionProps.Properties.Count; propertyIndex++) {
                        propName = functionProps.Properties[propertyIndex - 1].Value;
                        if(propertyIndex <= arguments.GetUpperBound(0)) {
                            propValue = arguments[propertyIndex - 1];
                        } else {
                            propValue = "";
                        }

                        // if(the varue is a link tag ... then get the linked variable name.
                        if(!Parser.Syntax.IsLinkTag(propValue, out varName)) varName = "";

                        Globals.MyEnvironment.Variables.Add(propName, propValue, false, false, Runtime.Variables.LocationTypes.Locale, varName, 0, localeLocationID, publicLocation, previousLocationID);
                    } //propertyIndex
                }
            } //AddFunctionArgs

            public void RemoveFunctionArgs(int instanceIndex, Framework.Properties.FunctionStatement functionProps)
            {
                string localeLocationID = Runtime.Variables.MakeLocationID(Runtime.Variables.LocationTypes.Locale, instanceIndex.ToString());

                // if(there are some properties then ...
                if(functionProps.Properties.Count > 0) {
                    string propName = "";
                    int variableIndex = 0;
                    // ... Go through each property ...
                    for(int propertyIndex = 1; propertyIndex <= functionProps.Properties.Count; propertyIndex++) {
                        propName = functionProps.Properties[propertyIndex - 1].Value;
                        variableIndex = Globals.MyEnvironment.Variables.FindIndex(propName, localeLocationID, localeLocationID, false, false);
                        //if(variableIndex > 0) Globals.MyEnvironment.Variables[variableIndex].locationType = DSSLRTLOCTYPE_NOTSET
                        Globals.MyEnvironment.Variables.Remove(1 + variableIndex, true, false, true);
                    } //propertyIndex
                }
            } //RemoveFunctionArgs

            public bool IsCompleted() { return (this.Count == 0); }

            /// <summary>Runs the script immediatly.</summary>
            /// <returns>If the script was a function, it's return results will be in this list.</returns>
            private List<string> Run()
            {
                List<string> results = new List<string>();

                // Start a loop ...
                do {
                    try {
                        // ... Continue the script, if done then ... exit this procedure.
                        if (!this.ContinueEvents(results)) break;
                    } catch {
                        // nothing to catch.
                    }
                } while(true);

                return results;
            } //Run

            public List<string> RunFromLine(Script script, int eventIndex, bool immediate)
            {
                this.Push(script, eventIndex - 1);

                if(immediate)
                    return this.Run();
                else 
                    return null;
            } //RunFromLine

            public List<string> RunFunction(Script script, string functionName, bool immediate, string[] arguments, out bool outCallIsGood)
            {
                outCallIsGood = false;

                if(script == null) return null;

                Script.ParsedFunction function = script.Parsed.Functions.Find(functionName);
                if(function == null) return null;

                return this.RunFunction(script, function, immediate, arguments, out outCallIsGood);
            } //RunFunction

            public List<string> RunFunction(Script script, string functionName, bool immediate, string arguments, out bool outCallIsGood)
            {
                return this.RunFunction(script, functionName, immediate, arguments.Split(','), out outCallIsGood);
            } //RunFunction

            public List<string> RunFunction(Script script, Script.ParsedFunction function, bool immediate, string[] arguments, out bool outCallIsGood)
            {
                outCallIsGood = false;

                // Clear the results.
                List<string> results = new List<string>();

                // Check for any 'out of bounds', if so ... exit with error.
                if(script == null || function == null) return null;

                // Get the event index.
                int eventIndex = function.EventIndex;

                // Get the previous instance ID.
                string strPrevInstanceID = "";
                if(this.Count > 0) strPrevInstanceID = this.mItem[this.Count - 1].InstanceID;
                // Get the public (script) instance ID.
                string strPublicLocationID = script.MakeLocationID();

                // Get the instance index.
                int dwInstance = this.Push(script, eventIndex);

                // Store the function's index.
                this.mItem[dwInstance - 1].Function = function;

                // Add the function's arguments into the private variables collection.
                this.AddFunctionArgs(dwInstance, function.Desc, arguments, strPrevInstanceID, strPublicLocationID);

                // if(we're running immediate then ...
                outCallIsGood = true;
                if(immediate)
                    return this.Run();
                else
                    return null;
            } //RunFunction

            public bool ContinueEvents(List<string> lastResult, bool skipLines = false)
            {
                // if(no instances are loaded then ... exit this procedure.
                if(this.Count == 0) return false;
                int dwInstance = this.Count;
                if(this.mItem[dwInstance - 1].Finished) return this.EndLastInstance();

                // Retreive the current instance's scripting file index.
                Script script = this.mItem[dwInstance - 1].Script;

                // Go to the next event line.
                this.mItem[dwInstance - 1].EventIndex += 1;
                // if(the event lines is passed the end then ...
                if(this.mItem[dwInstance - 1].EventIndex > script.Desc.Events.Count) {
                    // ... Make sure the event line index does not go out of the boundry ...
                    this.mItem[dwInstance - 1].EventIndex = script.Desc.Events.Count;
                    // ... Clear this instance as finished ...
                    this.mItem[dwInstance - 1].Finished = true;
                }

                // if(this instance is not completed then ...
                if(this.mItem[dwInstance - 1].Finished) {
                    return this.EndLastInstance();
                } else {
                    string strLocationID = this.mItem[dwInstance - 1].InstanceID;
                    this.Continue_InPlay(strLocationID, lastResult, skipLines);
                }

                // return successful (as in the script can continue).
                return true;
            } //ContinueEvents

            public bool ContinueEvents()
            {
                List<string> results = new List<string>();
                return this.ContinueEvents(results, false);
            } //ContinueEvents

            private void Continue_InPlay(string locationID, List<string> lastResult, bool skipLines = false)
            {
                int dwIndex = this.Count;
                
                // Get the current event.
                Framework.Event dEvent = this.mItem[dwIndex - 1].GetEventDesc();

                // Get the current command.
                Framework.EventCommands eCommand = dEvent.Command;

                // Get the current instance's location ID.
                string strLocationID = this.mItem[dwIndex - 1].InstanceID;

                // if(this command is a function then ...
                if(eCommand == Framework.EventCommands.Function) {
                    // ... Do the commands for the function.
                    Continue_DoFunction(dEvent, locationID, this.mItem[dwIndex - 1].GetCurPointers(), out lastResult, skipLines);
                } else if(eCommand == Framework.EventCommands.IfStatement) {
                    Continue_DoIfStatement(dEvent, locationID, skipLines);
                } else if(eCommand == Framework.EventCommands.DecisionCondition) {
                    Continue_DoDecisionCondition(dEvent, locationID, skipLines);
                } else if(eCommand == Framework.EventCommands.LoopStatement) {
                    Continue_DoLoop(dEvent, dwIndex, skipLines);
                } else if(eCommand == Framework.EventCommands.ScriptID) {
                    if(skipLines) return;
                    // set the script id.
                } else if(eCommand == Framework.EventCommands.DoExpression) {
                    if(skipLines) return;
                    string strPublicID = this.mItem[dwIndex - 1].Script.MakeLocationID();

                    lastResult.Clear();
                    lastResult.Add(Continue_DoExpression(dEvent, locationID, strPublicID));
                } else if(eCommand == Framework.EventCommands.External) {
                    if(skipLines) return;
                    if(!Settings.DontAllowExternalFunctions) {
                        Continue_DoExternal(dEvent, this.mItem[dwIndex - 1].GetCurPointers(), locationID, this.mItem[dwIndex - 1].Script);
                    }
                } else if(eCommand == Framework.EventCommands.VariableCommand) {
                    if(skipLines) return;

                    Continue_DoVarCommand(dEvent, this.mItem[dwIndex - 1].GetCurPointers(), strLocationID, this.mItem[dwIndex - 1].Script);
                } else if(eCommand == Framework.EventCommands.BookmarkCommand) {
                    if(skipLines) return;
                    Continue_DoBookMark(dEvent, locationID);
                } else if(eCommand == Framework.EventCommands.Stop) {
                    if(skipLines) return;
                    this.DoStop();
                } else if(eCommand == Framework.EventCommands.Include) {
                    if(skipLines) return;
                    Continue_DoInclude(dEvent, locationID);
                }
            } //Continue_InPlay

            private void Continue_DoDecisionCondition(Framework.Event eventLine, string locationID, bool skipLines = false)
            {
                // Get this function's properties.
                Framework.Properties.DecisionStatement dProps = new Framework.Properties.DecisionStatement();
                dProps.Parse(eventLine);

                Script script = this.mItem[this.Count - 1].Script;

                Framework.Event dEvent = new Framework.Event();
                Framework.Properties.DecisionStatement dProps2 = new Framework.Properties.DecisionStatement();

                if(dProps.Type == Parser.DecisionTypes.Start) {
                    // README: Once a condition statement is entered, the decision expression needs
                    //         to be tested with each and every decide item, if any matches, stop
                    //         there, if none matches, find the default, if the default doesn't
                    //         exist, just exit.

                    // ... Go through each line following ...
                    for(int dwEvent = this.mItem[this.Count - 1].EventIndex + 1; dwEvent <= this.mItem[this.Count - 1].GetEventLinesCount(); dwEvent++) {
                        // ... Get the infomation about this line ...
                        dEvent = this.mItem[this.Count - 1].GetEventDesc(dwEvent);
                        // ... if(this line is a function then ...
                        if(dEvent.Command == DSSL.Framework.EventCommands.DecisionCondition) {
                            // ... Get the function's properties ...
                            dProps2.Parse(dEvent);

                            // ... if(the line is the end of the function then ...
                            if(!skipLines && dProps2.Type == Parser.DecisionTypes.Item) {
                                if(this.CompareParameters(dProps.Value, dProps2.Value, dProps2.Comparison, this.mItem[this.Count - 1].GetCurPointers(), locationID, script)) {
                                    // ... Continue the script after this line ...
                                    this.mItem[this.Count - 1].SetCurEventIndex(dwEvent);
                                    // ... Exit this procedure.
                                    return;
                                }
                            } else if(!skipLines && dProps2.Type == Parser.DecisionTypes.Default) {
                                // ... Continue the script after this line ...
                                this.mItem[this.Count - 1].SetCurEventIndex(dwEvent);
                                // ... Exit this procedure.
                                return;
                            } else if(dProps2.Type == Parser.DecisionTypes.End) {
                                // ... Continue the script after this line ...
                                this.mItem[this.Count - 1].SetCurEventIndex(dwEvent);
                                // ... Exit this procedure.
                                return;
                            }
                        }
                    } // dwEvent
                } else if(dProps.Type == Parser.DecisionTypes.Item || dProps.Type == Parser.DecisionTypes.Default) {
                    // README: When a decision item or a decision else has been entered this means the current
                    //         decision block has been ran and now we need to find the end if.

                    // ... Go through each line following ...
                    for(int dwEvent = this.mItem[this.Count - 1].EventIndex + 1; dwEvent <= this.mItem[this.Count - 1].GetEventLinesCount(); dwEvent++) {
                        // ... Get the infomation about this line ...
                        dEvent = this.mItem[this.Count - 1].GetEventDesc(dwEvent);
                        // ... if(this line is a function then ...
                        if(dEvent.Command == DSSL.Framework.EventCommands.DecisionCondition) {
                            // ... Get the function's properties ...
                            dProps2.Parse(dEvent);
                            // ... if(the line is the end of the function then ...
                            if(dProps2.Type == Parser.DecisionTypes.End) {
                                // ... Continue the script after this line ...
                                this.mItem[this.Count - 1].SetCurEventIndex(dwEvent);
                                // ... Exit this procedure.
                                return;
                            }
                        }
                    } // dwEvent
                } else if(dProps.Type == Parser.DecisionTypes.End) {
                    // ... Continue normally.
                }
            } //Continue_DoDecisionCondition

            private void Continue_DoLoop(Framework.Event eventLine, int instanceIndex, bool skipLines = false)
            {
                // Get this loop's properties.
                Framework.Properties.LoopStatement dProps = new Framework.Properties.LoopStatement();
                dProps.Parse(eventLine);

                if(dProps.Type == Parser.LoopTypes.End) {
                    // ... Go through each line following ...
                    Framework.Event dEvent;
                    int dwInLoops = 0;
                    for(int dwEvent = this.mItem[instanceIndex - 1].EventIndex - 1; dwEvent >= 1; dwEvent--) {
                        // ... Get the infomation about this line ...
                        dEvent = this.mItem[instanceIndex - 1].GetEventDesc(dwEvent);
                        // ... if(this line is a loop then ...
                        if(dEvent.Command == Framework.EventCommands.LoopStatement) {
                            // ... Get the loop's properties ...
                            dProps.Parse(dEvent);
                            // ... if(the line is the end of the function then ...
                            if(dProps.Type == Parser.LoopTypes.Start) {
                                if(dwInLoops <= 0) {
                                    // ... Continue the script after this line ...
                                    this.mItem[instanceIndex - 1].SetCurEventIndex(dwEvent);
                                    // ... Exit this procedure.
                                    return;
                                } else {
                                    dwInLoops -= 1;
                                }
                            } else if(dProps.Type == Parser.LoopTypes.End) {
                                dwInLoops += 1;
                            }
                        }
                    } // dwEvent
                } else if((dProps.Type == Parser.LoopTypes.Start && skipLines) || dProps.Type == Parser.LoopTypes.Stop) {
                    // ... Go through each line following ...
                    Framework.Event dEvent;
                    int dwInLoops = 0;
                    for(int dwEvent = this.mItem[instanceIndex - 1].EventIndex + 1; dwEvent <= this.mItem[instanceIndex - 1].GetEventLinesCount(); dwEvent++) {
                        // ... Get the infomation about this line ...
                        dEvent = this.mItem[instanceIndex - 1].GetEventDesc(dwEvent);
                        // ... if(this line is a loop then ...
                        if(dEvent.Command == Framework.EventCommands.LoopStatement) {
                            // ... Get the loop's properties ...
                            dProps.Parse(dEvent);
                            // ... if(the line is the end of the function then ...
                            if(dProps.Type == Parser.LoopTypes.End) {
                                if(dwInLoops <= 0) {
                                    // ... Continue the script after this line ...
                                    this.mItem[instanceIndex - 1].SetCurEventIndex(dwEvent);
                                    // ... Exit this procedure.
                                    return;
                                } else {
                                    dwInLoops += 1;
                                }
                            } else if(dProps.Type == Parser.LoopTypes.Start) {
                                dwInLoops += 1;
                            }
                        }
                    } // dwEvent
                }
            } //Continue_DoLoop

            private void Continue_DoIfStatement(Framework.Event eventLine, string locationID, bool skipLines = false)
            {
                // Get this function's properties.
                Framework.Properties.IfStatement dIfStatement = new Framework.Properties.IfStatement();
                dIfStatement.Parse(eventLine);

                Script script = this.mItem[this.Count - 1].Script;

                if(dIfStatement.Type == Parser.IfStatementTypes.If) {
                    // README: Once an if statement has been entered, we need to test if it's true,
                    //         if not's true then we need to skip to any else or else ifs until one
                    //         of them is true, and if not then skip the whole entire if block.
                    //MsgBox "aaaa"

                    if(!skipLines && this.CompareParameters(dIfStatement.Value1, dIfStatement.Value2, dIfStatement.Compare, this.mItem[this.Count - 1].GetCurPointers(), locationID, script)) {
                        // if(it's true then continue normally.
                    } else {
                        //MsgBox "dog"
                        Framework.Event dEvent;
                        Framework.Properties.IfStatement dIfStatement2 = new Framework.Properties.IfStatement();
                        int dwInIfs = 0;

                        // ... Go through each line following ...
                        for(int dwEvent = this.mItem[this.Count - 1].EventIndex + 1; dwEvent <= this.mItem[this.Count - 1].GetEventLinesCount(); dwEvent++) {
                            // ... Get the infomation about this line ...
                            dEvent = this.mItem[this.Count - 1].GetEventDesc(dwEvent);
                            // ... if(this line is a function then ...
                            if(dEvent.Command == DSSL.Framework.EventCommands.IfStatement) {
                                //MsgBox "hog";
                                // ... Get the function's properties ...
                                dIfStatement2.Parse(dEvent);
                                // ... if(the line is the end of the function then ...
                                if(dIfStatement2.Type == Parser.IfStatementTypes.If) {
                                    dwInIfs += 1;
                                } else {
                                    if(!skipLines && dIfStatement2.Type == Parser.IfStatementTypes.ElseIf) {
                                        if(dwInIfs <= 0) {
                                            if(this.CompareParameters(dIfStatement2.Value1, dIfStatement2.Value2, dIfStatement2.Compare, this.mItem[this.Count - 1].GetCurPointers(), locationID, script)) {
                                                // ... Continue the script after this line ...
                                                this.mItem[this.Count - 1].SetCurEventIndex(dwEvent);
                                                // ... Exit this procedure.
                                                return;
                                            }
                                        }
                                    } else if(!skipLines && dIfStatement2.Type == Parser.IfStatementTypes.Else) {
                                        if(dwInIfs <= 0) {
                                            // ... Continue the script after this line ...
                                            this.mItem[this.Count - 1].SetCurEventIndex(dwEvent);
                                            // ... Exit this procedure.
                                            return;
                                        }
                                    } else if(dIfStatement2.Type == Parser.IfStatementTypes.EndIf) {
                                        if(dwInIfs <= 0) {
                                            // ... Continue the script after this line ...
                                            this.mItem[this.Count - 1].SetCurEventIndex(dwEvent);
                                            // ... Exit this procedure.
                                            return;
                                        } else {
                                            dwInIfs -= 1;
                                        }
                                    }
                                }
                            }
                        } // dwEvent
                    }
                } else if(dIfStatement.Type == Parser.IfStatementTypes.ElseIf || dIfStatement.Type == Parser.IfStatementTypes.Else) {
                    // README: When a else if or a else has been entered this means the current if
                    //         block has been ran and now we need to find the end if.
                    Framework.Event dEvent;
                    Framework.Properties.IfStatement dIfStatement2 = new Framework.Properties.IfStatement();
                    int dwInIfs = 0;

                    // ... Go through each line following ...
                    for(int dwEvent = this.mItem[this.Count - 1].EventIndex + 1; dwEvent <= this.mItem[this.Count - 1].GetEventLinesCount(); dwEvent++) {
                        // ... Get the infomation about this line ...
                        dEvent = this.mItem[this.Count - 1].GetEventDesc(dwEvent);
                        // ... if(this line is a function then ...
                        if(dEvent.Command == DSSL.Framework.EventCommands.IfStatement) {
                            // ... Get the function's properties ...
                            dIfStatement2.Parse(dEvent);
                            // ... if(the line is the end of the function then ...
                            if(dIfStatement2.Type == Parser.IfStatementTypes.EndIf) {
                                if(dwInIfs <= 0) {
                                    // ... Continue the script after this line ...
                                    this.mItem[this.Count - 1].SetCurEventIndex(dwEvent);
                                    // ... Exit this procedure.
                                    return;
                                } else {
                                    dwInIfs -= 1;
                                }
                            } else if(dIfStatement2.Type == Parser.IfStatementTypes.If) {
                                dwInIfs += 1;
                            }
                        }
                    } // dwEvent
                } else if(dIfStatement.Type == Parser.IfStatementTypes.EndIf) {
                    // ... Continue normally.
                }
            } //Continue_DoIfStatement

            private List<string> Continue_DoEvent_StandAlone(Framework.Event desc, Framework.EventsCollection pointers, string locationID, Script inHomeScript = null)
            {
                // Get the current command.
                Framework.EventCommands eCommand = desc.Command;

                // if(this command is a function then ...
                if(eCommand == Framework.EventCommands.Function) {
                    return Continue_DoFunctionStandAlone(desc, locationID, pointers, inHomeScript);
                } else if(eCommand == Framework.EventCommands.IfStatement) {
                    // do nothing
                } else if(eCommand == Framework.EventCommands.LoopStatement) {
                    // do nothing
                } else if(eCommand == Framework.EventCommands.ScriptID) {
                    // return the script id
                } else if(eCommand == Framework.EventCommands.DoExpression) {
                    string strPublicID = "";
                    if(inHomeScript != null) strPublicID = inHomeScript.MakeLocationID();
                    List<string> result = new List<string>();
                    result.Add(Continue_DoExpression(desc, locationID, strPublicID));
                    return result;
                } else if(eCommand == Framework.EventCommands.External) {
                    if(!Settings.DontAllowExternalFunctions) return Continue_DoExternal(desc, pointers, locationID, inHomeScript);
                } else if(eCommand == Framework.EventCommands.VariableCommand) {
                    return Continue_DoVarCommand(desc, pointers, locationID, inHomeScript);
                }

                return null;
            } //Continue_DoEvent_StandAlone

            private void Continue_DoFunction(Framework.Event eventLine, string locationID, Framework.EventsCollection pointers, out List<string> outResults, bool skipLines = false)
            {
                outResults = new List<string>();
                string strPublicID = this.mItem[this.Count - 1].Script.MakeLocationID();

                // Get this function's properties.
                Framework.Properties.FunctionStatement dFunction = new Framework.Properties.FunctionStatement();
                dFunction.Parse(eventLine);

                if(dFunction.Type == Parser.FunctionTypes.Header) {
                    // ... Since a function cannot be entered unless it is called, we need to continue
                    // the script from the end of the function.
                    // ... Go through each line following ...
                    Framework.Event dEvent;
                    Framework.Properties.FunctionStatement dFunction2 = new Framework.Properties.FunctionStatement();
                    for(int dwEvent = this.mItem[this.Count - 1].EventIndex + 1; dwEvent <= this.mItem[this.Count - 1].GetEventLinesCount(); dwEvent++) {
                        // ... Get the infomation about this line ...
                        dEvent = this.mItem[this.Count - 1].GetEventDesc(dwEvent);
                        // ... if(this line is a function then ...
                        if(dEvent.Command == DSSL.Framework.EventCommands.Function) {
                            // ... Get the function's properties ...
                            dFunction2.Parse(dEvent);
                            // ... if(the line is the end of the function then ...
                            if(dFunction2.Type == Parser.FunctionTypes.End) {
                                // ... Continue the script after this line ...
                                this.mItem[this.Count - 1].SetCurEventIndex(dwEvent);
                                // ... Exit this procedure.
                                return;
                            }
                        }
                    } // dwEvent
                    // if(the function reached the end of it's block then ...
                } else if(dFunction.Type == Parser.FunctionTypes.End) {
                    // ... Declare this instance is finished.
                    this.mItem[this.Count - 1].Finished = true;
                    // if(the command wants to exit the current function then ...
                } else if(dFunction.Type == Parser.FunctionTypes.Exit) {
                    if(skipLines) return;
                    // ... Declare this instance is finished.
                    this.mItem[this.Count - 1].Finished = true;
                    // Set the results ...
                } else if(dFunction.Type == Parser.FunctionTypes.Results) {
                    if(skipLines) return;
                    // ... return the results.
                    //retResults = DSSL_Runtime_DoExpression(dFunction.name, locationID, strPublicID)
                    outResults.Clear();
                    if(dFunction.Results.Count > 0) {
                        for(int dwProp = 1; dwProp <= dFunction.Results.Count; dwProp++) {
                            outResults.Add(Runtime.Expression.Run(dFunction.Results[dwProp - 1], locationID, strPublicID));
                        } // dwProp
                    }
                    // Same as if we're going to end ...
                    // ... Declare this instance is finished.
                    this.mItem[this.Count - 1].Finished = true;
                    // another function ...
                } else if(dFunction.Type == Parser.FunctionTypes.Call) {
                    if(skipLines) return;
                    Script homeScript = this.mItem[this.Count - 1].Script;
                    Script foundScript = null;
                    Script.ParsedFunction function = Globals.MyEnvironment.Scripts.FindFunction(dFunction.Name, homeScript, out foundScript);

                    if(function != null) {
                        string[] aArgs = null;
                        if(dFunction.Properties.Count > 0) {
                            aArgs = new string[dFunction.Properties.Count];
                            for(int dwArg = 0; dwArg < dFunction.Properties.Count; dwArg++) {
                                aArgs[dwArg] = StringManager.GetQuoteString(this.GetEventParameterValue(dFunction.Properties[dwArg], pointers, locationID, homeScript));
                            } // dwArg
                        }

                        bool callGood = false;
                        this.RunFunction(foundScript, function, false, aArgs, out callGood);
                    }
                }
            } //Continue_DoFunction

            private List<string> Continue_DoFunctionStandAlone(Framework.Event eventLine, string locationID, Framework.EventsCollection pointers, Script inHomeScript = null)
            {
                // Get this function's properties.
                Framework.Properties.FunctionStatement dFunction = new Framework.Properties.FunctionStatement();
                dFunction.Parse(eventLine);

                // if(this fuction is the header then ...
                if(dFunction.Type == Parser.FunctionTypes.Header) {
                    // do nothing
                    // if(the function reached the end of it's block then ...
                } else if(dFunction.Type == Parser.FunctionTypes.End) {
                    // do nothing
                    // if(the command wants to exit the current function then ...
                } else if(dFunction.Type == Parser.FunctionTypes.Exit) {
                    // do nothing
                    // Set the results ...
                } else if(dFunction.Type == Parser.FunctionTypes.Results) {
                    // do nothing
                    // another function ...
                } else if(dFunction.Type == Parser.FunctionTypes.Call) {
                    Script functionScript = null;
                    Script.ParsedFunction function = Globals.MyEnvironment.Scripts.FindFunction(dFunction.Name, inHomeScript, out functionScript);

                    if(function != null) {
                        string[] aArgs;
                        if(dFunction.Properties.Count == 0) {
                            aArgs = new string[1];
                        } else {
                            aArgs = new string[dFunction.Properties.Count];
                            for(int dwArg = 0; dwArg < dFunction.Properties.Count; dwArg++) {
                                aArgs[dwArg] = StringManager.GetQuoteString(this.GetEventParameterValue(dFunction.Properties[dwArg], pointers, locationID, inHomeScript));
                            } // dwArg
                        }

                        // Run a new seperate path for this standalone functions.
                        bool goodCall = false;
                        Instances cInstances = new Instances();
                        return cInstances.RunFunction(functionScript, function, true, aArgs, out goodCall);
                    }
                }

                return null;
            } //Continue_DoFunctionStandAlone

            private void Continue_DoBookMark(Framework.Event eventLine, string locationID)
            {
                // Get this loop's properties.
                Framework.Properties.BookmarkStatement dProps = new Framework.Properties.BookmarkStatement();
                dProps.Parse(eventLine);

                string strSrcName = this.GetEventParameterValue(dProps.Name, this.mItem[this.Count - 1].GetCurPointers(), locationID, this.mItem[this.Count - 1].Script);

                if(dProps.Type == Parser.BookmarkTypes.Set) {
                    // do nothing
                } else if(dProps.Type == Parser.BookmarkTypes.GoTo) {
                    int dwEvents = this.mItem[this.Count - 1].GetEventLinesCount();

                    string strDestName = "";
                    Framework.Event dEvent = null;
                    // ... Go through each line following ...
                    for(int dwEvent = 1; dwEvent <= dwEvents; dwEvent++) {
                        // ... Get the infomation about this line ...
                        dEvent = this.mItem[this.Count - 1].GetEventDesc(dwEvent);
                        // ... if(this line is a loop then ...
                        if(dEvent.Command == DSSL.Framework.EventCommands.BookmarkCommand) {
                            // ... Get the loop's properties ...
                            dProps.Parse(dEvent);

                            // ... if(the line is the end of the function then ...
                            if(dProps.Type == Parser.BookmarkTypes.Set) {
                                strDestName = this.GetEventParameterValue(dProps.Name, this.mItem[this.Count - 1].GetCurPointers(), locationID, this.mItem[this.Count - 1].Script);
                                if(string.Equals(strSrcName, strDestName, System.StringComparison.CurrentCultureIgnoreCase)) {
                                    // ... Continue the script after this line ...
                                    this.mItem[this.Count - 1].SetCurEventIndex(dwEvent);
                                    // ... Exit this procedure.
                                    return;
                                }
                            }
                        }
                    } // dwEvent
                }
            } //Continue_DoBookMark

            private void Continue_DoInclude(Framework.Event eventLine, string locationID)
            {
                // Get this line's properties.
                Framework.Properties.IncludeStatement dProps = new Framework.Properties.IncludeStatement();
                dProps.Parse(eventLine);

                // Get the home script.
                Script script = this.mItem[this.Count - 1].Script;

                // Get the file name being used (in string format)
                string strFileName = this.GetEventParameterValue(dProps.FileName, this.mItem[this.Count - 1].GetCurPointers(), locationID, script);
                strFileName = FileManager.FindIncludeFile(strFileName, FileManager.GetPathName(script.Desc.FileName), Settings.LibraryPath);

                // Add the script to the collection.
                Globals.MyEnvironment.Scripts.Add(strFileName);
            } //Continue_DoInclude

            private List<string> Continue_DoExternal(Framework.Event eventLine, Framework.EventsCollection pointers, string locationID, Script homeScript = null)
            {
                // Get this function's properties.
                Framework.Properties.ExternalStatement dExternal = new Framework.Properties.ExternalStatement();
                dExternal.Parse(eventLine);

                List<string> args = null;
                if(!string.IsNullOrEmpty(dExternal.ProcName)) {
                    if(dExternal.Arguments.Count > 0) {
                        for(int dwArg = 0; dwArg < dExternal.Arguments.Count; dwArg++) {
                            args.Add(this.GetEventParameterValue(dExternal.Arguments[dwArg].Value, pointers, locationID, homeScript));
                        } // dwArg
                    }

                    if (Globals.MyHelper != null) return Globals.MyHelper.DoCommand(dExternal.ModuleName, dExternal.ProcName, args);
                }

                return null;
            } //Continue_DoExternal

            private List<string> Continue_DoVarCommand(Framework.Event eventLine, Framework.EventsCollection pointers, string localeLocationID = "", Script currentScript = null)
            {
                string publicLocationID = currentScript.MakeLocationID();

                // Get this function's properties.
                Framework.Properties.VariableCommandStatement dProps = new Framework.Properties.VariableCommandStatement();
                dProps.Parse(eventLine);
                
                if(dProps.Type == Parser.VariableTypes.Add) {
                    if(dProps.Names.Count > 0 && dProps.Values.Count > 0) {
                        Runtime.Variables.LocationTypes locationType = Runtime.Variables.ScopeToLocationType(dProps.Scope);
                        Runtime.Variables.LocationTypes determinedType;
                        string determinedLocationParent;
                        string addLocation = Runtime.Variables.DetermineLocationID(locationType, localeLocationID, publicLocationID, out determinedType, out determinedLocationParent);
                        string value = this.GetEventParameterValue(dProps.Values[0], pointers, localeLocationID, currentScript);

                        Globals.MyEnvironment.Variables.Add(dProps.Names[0].Name, value, false, true, locationType, "", System.Convert.ToInt32(dProps.Names[0].Index), addLocation, publicLocationID);
                    }
                } else if(dProps.Type == Parser.VariableTypes.Set) {
                    if(dProps.Names.Count > 0 && dProps.Values.Count > 0) {
                        string value = "";
                        List<string> values = new List<string>();
                        for(int index = 0; index < dProps.Names.Count; index++) {
                            value = "";
                            if(dProps.Values.Count == 1) {
                                values = this.GetEventParameterValues(dProps.Values[0], pointers, localeLocationID, currentScript);
                                if(values.Count == 1) {
                                    value = values[0];
                                } else if(index < values.Count) {
                                    value = values[index];
                                }
                            } else if(index < dProps.Values.Count) {
                                value = this.GetEventParameterValue(dProps.Values[index], pointers, localeLocationID, currentScript);
                            }

                            Globals.MyEnvironment.Variables.SetValue(dProps.Names[index].Name, value, System.Convert.ToInt32(dProps.Names[index].Index), localeLocationID, publicLocationID);
                        } // dwIndex
                    }
                } else if(dProps.Type == Parser.VariableTypes.Get) {
                    if(dProps.Names.Count > 0) {
                        List<string> results = new List<string>();
                        results.Add(Globals.MyEnvironment.Variables.GetValue(dProps.Names[0].Name, localeLocationID, dProps.Default, publicLocationID, System.Convert.ToInt32(dProps.Names[0].Index)));
                        return results;
                    }
                } else if(dProps.Type == Parser.VariableTypes.Remove) {
                    //DSSL_RuntimeVars_Remove(dProps.name, locationID, , strPublicLocation)
                    if(dProps.Names.Count > 0) {
                        for(int index = 0; index < dProps.Names.Count; index++) {
                            Globals.MyEnvironment.Variables.Remove(dProps.Names[index].Name, localeLocationID, true, publicLocationID);
                        } // dwIndex
                    }
                }

                return null;
            } //Continue_DoVarCommand

            private string Continue_DoExpression(Framework.Event eventLine, string locationID = "", string publicLocationID = "")
            {
                // Get this function's properties.
                Framework.Properties.DoExpressionStatement dProps = new Framework.Properties.DoExpressionStatement();
                dProps.Parse(eventLine);

                // Run the expression in the line.
                return Runtime.Expression.Run(dProps.Expression, locationID, publicLocationID);
            } //Continue_DoExpression

            private bool CompareParameters(Framework.Parameter a, Framework.Parameter b, Parser.Comparisons comparision, Framework.EventsCollection pointers, string locationID, Script homeScript = null)
            {
                string strA = this.GetEventParameterValue(a, pointers, locationID, homeScript);
                string strB = this.GetEventParameterValue(b, pointers, locationID, homeScript);

                bool isNumericA = Tools.IsNumeric(strA);
                bool isNumericB = Tools.IsNumeric(strB);
                bool isNumericComparision = (isNumericA && isNumericB);
                
                if(isNumericComparision) {
                    if(comparision == Parser.Comparisons.Equals) {
                        if(System.Convert.ToInt32(strA) == System.Convert.ToInt32(strB)) return true;
                    } else if(comparision == Parser.Comparisons.NotEquals) {
                        if(System.Convert.ToInt32(strA) != System.Convert.ToInt32(strB)) return true;
                    } else if(comparision == Parser.Comparisons.Less) {
                        if(System.Convert.ToInt32(strA) < System.Convert.ToInt32(strB)) return true;
                    } else if(comparision == Parser.Comparisons.More) {
                        if(System.Convert.ToInt32(strA) > System.Convert.ToInt32(strB)) return true;
                    } else if(comparision == Parser.Comparisons.EqualsOrLess) {
                        if(System.Convert.ToInt32(strA) <= System.Convert.ToInt32(strB)) return true;
                    } else if(comparision == Parser.Comparisons.EqualsOrMore) {
                        if(System.Convert.ToInt32(strA) >= System.Convert.ToInt32(strB)) return true;
                    }
                } else {
                    if(comparision == Parser.Comparisons.Equals) {
                        if(string.Equals(strA, "#true#", System.StringComparison.CurrentCultureIgnoreCase)) {
                            if(isNumericB) {
                                return (System.Convert.ToInt32(strB) != 0);
                            } else {
                                return !string.IsNullOrEmpty(strB);
                            }
                        } else if(string.Equals(strB, "#true#", System.StringComparison.CurrentCultureIgnoreCase)) {
                            if(isNumericA) {
                                return (System.Convert.ToInt32(strA) != 0);
                            } else {
                                return !string.IsNullOrEmpty(strA);
                            }
                        } else if(string.Equals(strA, strB)) {
                            return true;
                        }
                    } else if(comparision == Parser.Comparisons.NotEquals) {
                        if(string.Equals(strA, "#true#", System.StringComparison.CurrentCultureIgnoreCase)) {
                            if(isNumericB) {
                                return (System.Convert.ToInt32(strB) == 0);
                            } else {
                                return string.IsNullOrEmpty(strB);
                            }
                        } else if(string.Equals(strB, "#true#", System.StringComparison.CurrentCultureIgnoreCase)) {
                            if(isNumericA) {
                                return (System.Convert.ToInt32(strA) == 0);
                            } else {
                                return string.IsNullOrEmpty(strA);
                            }
                        } else if(string.Equals(strA, strB)) {
                            return true;
                        }
                    } else if(comparision == Parser.Comparisons.Less) {
                        // not supported
                    } else if(comparision == Parser.Comparisons.More) {
                        // not supported
                    } else if(comparision == Parser.Comparisons.EqualsOrLess) {
                        // not supported
                    } else if(comparision == Parser.Comparisons.EqualsOrMore) {
                        // not supported
                    }
                }

                return false;
            } //CompareParameters

            private string GetEventParameterValue(Framework.Parameter desc, Framework.EventsCollection pointers, string locationID, Script homeScript)
            {
                List<string> results = new List<string>();
                results = this.GetEventParameterValues(desc, pointers, locationID, homeScript);
                
                if(results != null && results.Count > 0)
                    return results[0];
                else
                    return "";
            } //GetEventParameterValue

            private List<string> GetEventParameterValues(Framework.Parameter desc, Framework.EventsCollection pointers, string locationID, Script homeScript)
            {
                if(desc.Type == Framework.EventParameterTypes.Value) {
                    string runResults = Runtime.Expression.Run(desc.Value, locationID, homeScript.MakeLocationID());
                    if(!string.IsNullOrEmpty(runResults)) {
                        List<string> results = new List<string>();
                        results.Add(runResults);
                        return results;
                    }
                } else if(desc.Type == Framework.EventParameterTypes.Function) {
                    int pointerIndex = pointers.FindByPointerID(desc.Value);
                    if(pointerIndex > 0) return this.Continue_DoEvent_StandAlone(pointers[pointerIndex - 1], pointers, locationID, homeScript);
                }

                return null;
            } //GetEventParameterValues
        } //Instances
    } //Runtime

    public class SDK
    {
        private static string FunctionPropertiesToString(List<Framework.Parameter> items, Framework.EventsCollection pointers, int start = 1)
        {
            string results = "";

            if(items != null && items.Count > 0) {
                for(int index = start; index <= (items.Count - (1 - start)); index++) {
                    results = (string.IsNullOrEmpty(results) ? "" : results + ", ") + "\"" + GetParameterID(items[index - 1], pointers) + "\"";
                } // dwIndex
            }

            return results;
        } //FunctionPropertiesToString

        private static string VariableValuesToString(Framework.Properties.VariableCommandValuesStatement desc, Framework.EventsCollection pointers)
        {
            string results = "";
            
            if(desc != null && desc.Count > 0) {
                for(int index = 0; index < desc.Count; index++) {
                    results = (string.IsNullOrEmpty(results) ? "" : results + ", ") + "\"" + GetParameterID(desc[index], pointers) + "\"";
                } // dwIndex
            }

            return results;
        } //VariableValuesToString

        private static string ExternalArgumentsToString(Framework.Properties.ExternalArgumentsStatement desc, Framework.EventsCollection pointers)
        {
            string results = "";

            if(desc.Count > 0) {
                for(int index = 0; index < desc.Count; index++) {
                    results = (string.IsNullOrEmpty(results) ? "" : results + ", ") + "\"" + GetParameterID(desc[index].Value, pointers) + "\"";
                } // dwIndex
            }

            return results;
        } //ExternalArgumentsToString

        private static string GetParameterID(Framework.Parameter desc, Framework.EventsCollection pointers)
        {
            if(desc.Type == Framework.EventParameterTypes.Value) {
                return desc.Value;
            } else if(desc.Type == Framework.EventParameterTypes.Function) {
                int pointerIndex = pointers.FindByPointerID(desc.Value);

                if(pointerIndex < 1) {
                    return "(Invalid Pointer: " + desc.Value + ")";
                } else {
                    return GetEventID(pointers[pointerIndex - 1], pointers);
                }
            } else {
                return "";
            }
        } //GetParameterID

        public static string GetEventID(Framework.Event eventDesc, Framework.EventsCollection pointers)
        {
            string strLeftMark = "";
            if(eventDesc.Disabled) strLeftMark = "*";

            Framework.Properties.CommentStatement      dComment      = new Framework.Properties.CommentStatement();
            Framework.Properties.IfStatement  dIf           = new Framework.Properties.IfStatement();
            Framework.Properties.LoopStatement         dLoop         = new Framework.Properties.LoopStatement();
            Framework.Properties.ExternalStatement     dExternal     = new Framework.Properties.ExternalStatement();
            Framework.Properties.DoExpressionStatement dDoExpression = new Framework.Properties.DoExpressionStatement();
            Framework.Properties.VariableCommandStatement   dVarCommand   = new Framework.Properties.VariableCommandStatement();
            Framework.Properties.BookmarkStatement     dBookMark     = new Framework.Properties.BookmarkStatement();
            Framework.Properties.DecisionStatement     dDecision     = new Framework.Properties.DecisionStatement();
            Framework.Properties.IncludeStatement      dInclude      = new Framework.Properties.IncludeStatement();
            Framework.Properties.FunctionStatement     dFunction     = new Framework.Properties.FunctionStatement();

            switch(eventDesc.Command) {
                case(Framework.EventCommands.Nothing): return strLeftMark;
                case(Framework.EventCommands.Comment):
                    dComment.Parse(eventDesc);
                    return strLeftMark + "# " + dComment.Text;
                case(Framework.EventCommands.Function):
                    dFunction.Parse(eventDesc);
                    if(dFunction.Type == Parser.FunctionTypes.Header) {
                        return strLeftMark + Parser.Translator.FunctionAccessToString(dFunction.Access) + " Function " + dFunction.Name + " (" + FunctionPropertiesToString(dFunction.Properties, pointers) + ")";
                    } else if(dFunction.Type == Parser.FunctionTypes.Results) {
                        return strLeftMark + "Return (" + StringManager.ArrayToString(dFunction.Results, 1, true) + ")";
                    } else if(dFunction.Type == Parser.FunctionTypes.Exit) {
                        return strLeftMark + "Exit Function";
                    } else if(dFunction.Type == Parser.FunctionTypes.End) {
                        return strLeftMark + "} //";
                    } else if(dFunction.Type == Parser.FunctionTypes.Call) {
                        return strLeftMark + "Function " + dFunction.Name + "(" + FunctionPropertiesToString(dFunction.Properties, pointers) + ")";
                    } else {
                        return strLeftMark + "Unknown Function Type (" + dFunction.Type + ")";
                    }
                case(Framework.EventCommands.IfStatement):
                    dIf.Parse(eventDesc);
                    if(dIf.Type == Parser.IfStatementTypes.If) {
                        return strLeftMark + "If (" + GetParameterID(dIf.Value1, pointers) + ") " + Parser.Translator.ComparisonToString(dIf.Compare) + " (" + GetParameterID(dIf.Value2, pointers) + ")) {";
                    } else if(dIf.Type == Parser.IfStatementTypes.ElseIf) {
                        return strLeftMark + "Else if((" + GetParameterID(dIf.Value1, pointers) + ") " + Parser.Translator.ComparisonToString(dIf.Compare) + " (" + GetParameterID(dIf.Value2, pointers) + ")) {";
                    } else if(dIf.Type == Parser.IfStatementTypes.Else) {
                        return strLeftMark + "Else";
                    } else if(dIf.Type == Parser.IfStatementTypes.EndIf) {
                        return strLeftMark + "End If";
                    } else {
                        return strLeftMark + "Unknown if(Statement Type (" + dIf.Type + ")";
                    }
                case(Framework.EventCommands.LoopStatement):
                    dLoop.Parse(eventDesc);
                    if(dLoop.Type == Parser.LoopTypes.Start) {
                        return strLeftMark + "Start Loop";
                    } else if(dLoop.Type == Parser.LoopTypes.Stop) {
                        return strLeftMark + "Stop Loop";
                    } else if(dLoop.Type == Parser.LoopTypes.End) {
                        return strLeftMark + "End Loop";
                    } else {
                        return strLeftMark + "Unknown Loop Type (" + dLoop.Type + ")";
                    }
                case(Framework.EventCommands.External):
                    dExternal.Parse(eventDesc);
                    return strLeftMark + "" + (string.IsNullOrEmpty(dExternal.ModuleName) ? "" : dExternal.ModuleName + ".") + dExternal.ProcName + " (" + ExternalArgumentsToString(dExternal.Arguments, pointers) + ")";
                case(Framework.EventCommands.ScriptID):
                    //Dim dScriptID As Framework.Properties.SCRIPTID_DESC
                    //DSSL_Runtime_GetProperties_script(eventDesc, dScriptID)
                    return strLeftMark + "(Script ID)";
                case(Framework.EventCommands.DoExpression):
                    dDoExpression.Parse(eventDesc);
                    return strLeftMark + "Do Expression (" + dDoExpression.Expression + ")";
                case(Framework.EventCommands.VariableCommand):
                    dVarCommand.Parse(eventDesc);
                    if(dVarCommand.Type == Parser.VariableTypes.Add) {
                        return strLeftMark + "Add " + Parser.Translator.VariableScopeToString(dVarCommand.Scope) + " Variables " + "(" + dVarCommand.Names.ToString() + ") to Values (" + VariableValuesToString(dVarCommand.Values, pointers) + ")";
                    } else if(dVarCommand.Type == Parser.VariableTypes.Set) {
                        return strLeftMark + "Set Variables " + "(" + dVarCommand.Names.ToString() + ") to Values (" + VariableValuesToString(dVarCommand.Values, pointers) + ")";
                    } else if(dVarCommand.Type == Parser.VariableTypes.Remove) {
                        return strLeftMark + "Remove Variable (" + dVarCommand.Names.ToString() + ")";
                    } else if(dVarCommand.Type == Parser.VariableTypes.Get) {
                        return strLeftMark + "Get Variable (" + dVarCommand.Names.ToString() + ")";
                    } else {
                        return strLeftMark + "Unknown Variable Command (" + dVarCommand.Type + ")";
                    }
                case(Framework.EventCommands.BookmarkCommand):
                    dBookMark.Parse(eventDesc);
                    if(dBookMark.Type == Parser.BookmarkTypes.GoTo) {
                        return strLeftMark + "Goto Book Mark (" + GetParameterID(dBookMark.Name, pointers) + ")";
                    } else if(dBookMark.Type == Parser.BookmarkTypes.Set) {
                        return strLeftMark + "Set Book Mark (" + GetParameterID(dBookMark.Name, pointers) + ")";
                    } else {
                        return strLeftMark + "Unknown Book Mark Command (" + dBookMark.Type + ")";
                    }
                case(Framework.EventCommands.Stop): return strLeftMark + "Stop Execution";
                case(Framework.EventCommands.DecisionCondition):
                    dDecision.Parse(eventDesc);
                    if(dDecision.Type == Parser.DecisionTypes.Start) {
                        return strLeftMark + "Decide (" + GetParameterID(dDecision.Value, pointers) + ")";
                    } else if(dDecision.Type == Parser.DecisionTypes.Item) {
                        return strLeftMark + "Decide Is " + Parser.Translator.ComparisonToString(dDecision.Comparison) + " " + GetParameterID(dDecision.Value, pointers);
                    } else if(dDecision.Type == Parser.DecisionTypes.Default) {
                        return strLeftMark + "Decide Default";
                    } else if(dDecision.Type == Parser.DecisionTypes.End) {
                        return strLeftMark + "End Decide";
                    } else {
                        return strLeftMark + "Unknown Decision Condition (" + dDecision.Type + ")";
                    }
                case(Framework.EventCommands.Include):
                    dInclude.Parse(eventDesc);
                    return strLeftMark + "Include Script(" + GetParameterID(dInclude.FileName, pointers) + ")";
                default:
                    return strLeftMark + "Unknown Command (" + eventDesc.Command + ")";
            }
        } //GetEventID

        public static int GetIndentCount(Framework.EventsCollection eventsDesc, int eventIndex)
        {
            if(eventIndex < 2) eventIndex = 2;
            if(eventIndex > eventsDesc.Count) eventIndex = eventsDesc.Count;
            if(eventIndex == 0 || eventsDesc.Count < 2) return 0;

            Framework.Properties.FunctionStatement    dFunction = new Framework.Properties.FunctionStatement();
            Framework.Properties.IfStatement dIf       = new Framework.Properties.IfStatement();
            Framework.Properties.LoopStatement        dLoop     = new Framework.Properties.LoopStatement();

            int results = 0;
            for (int dwEvent = 1; dwEvent < eventIndex; dwEvent++)
            {
                if(eventsDesc[dwEvent - 1].Command == Framework.EventCommands.Function) {
                    dFunction.Parse(eventsDesc[dwEvent - 1]);
                    if(dFunction.Type == Parser.FunctionTypes.Header) results += 1;
                } else if(eventsDesc[dwEvent - 1 ].Command == Framework.EventCommands.IfStatement) {
                    dIf.Parse(eventsDesc[dwEvent - 1]);
                    if(dIf.Type == Parser.IfStatementTypes.If) results += 1;
                } else if(eventsDesc[dwEvent - 1].Command == Framework.EventCommands.LoopStatement) {
                    dLoop.Parse(eventsDesc[dwEvent - 1]);
                    if(dLoop.Type == Parser.LoopTypes.Start) results += 1;
                }

                if(eventsDesc[dwEvent].Command == Framework.EventCommands.Function) {
                    dFunction.Parse(eventsDesc[dwEvent]);
                    if(dFunction.Type == Parser.FunctionTypes.End) results -= 1;
                } else if(eventsDesc[dwEvent].Command == Framework.EventCommands.IfStatement) {
                    dIf.Parse(eventsDesc[dwEvent]);
                    if(dIf.Type == Parser.IfStatementTypes.ElseIf || dIf.Type == Parser.IfStatementTypes.Else || dIf.Type == Parser.IfStatementTypes.EndIf) {
                        results -= 1;
                    }
                } else if(eventsDesc[dwEvent].Command == Framework.EventCommands.LoopStatement) {
                    dLoop.Parse(eventsDesc[dwEvent]);
                    if(dLoop.Type == Parser.LoopTypes.End) results -= 1;
                }
            } // eventIndex

            return results;
        } //GetIndentCount
    } //SDK

    public class ErrorsManager : List<ErrorItem>
    {
        public int LastLine;

        public ErrorItem Add(string description, int lineNumber = -1)
        {
            base.Add(new ErrorItem(base.Count + 1, description, lineNumber));
            return base[base.Count - 1];
        } //Add
    } //ErrorsManager class

    public class ErrorItem
    {
        public int    Index;
        public string Description;
        public int    Line;

        public ErrorItem() {}

        public ErrorItem(int index, string description, int line)
        {
            this.Index       = index;
            this.Description = description;
            this.Line        = line;
        }//Constructor
    } //ErrorItem class

    public static class FileManager
    {
        public static string GetFileContentsString(string fileName)
        {
            if(!System.IO.File.Exists(fileName)) return "";

            //TODO: Debug the following:
            System.IO.StreamReader fsSource = System.IO.File.OpenText(fileName);
            string result = fsSource.ReadToEnd();
            fsSource.Close();
            return result;

            //Dim aBuffer() As Byte, dwSize As Long = 0
            //aBuffer = GetFileContentsArray(fileName, dwSize)

            //TODO: Debug the following line.
            //GetFileContentsString = System.Text.UnicodeEncoding.Unicode.GetString(aBuffer)
            //GetFileContentsString = System.Text.UnicodeEncoding.ASCII.GetString(aBuffer)
        } //GetFileContentsString

        public static string RemoveDirSep(string pathName)
        {
            if(pathName.EndsWith("\\") || pathName.EndsWith("/")) {
                return pathName.Substring(0, pathName.Length - 1);
            } else {
                return pathName;
            }
        } //RemoveDirSep function

        public static string GetWindowsDir()
        {
            return System.IO.Path.GetDirectoryName(System.Environment.SystemDirectory);
        } //GetWindowsDir

        public static string CalculatePath(string pathName, string startPath)
        {
            if(string.IsNullOrEmpty(pathName)) return "";

            if(pathName.StartsWith(".\\")) { //current path
                pathName = pathName.Replace(".\\", "");
                return System.IO.Path.Combine(startPath, pathName);
            } else if(pathName.StartsWith("\\")) { //root path
                // ... Using the start path, return it's root ...
                startPath = System.IO.Path.GetPathRoot(startPath);
                pathName = pathName.Substring(1);
                return System.IO.Path.Combine(startPath, pathName);
            } else {
                if(!pathName.Contains(":\\")) {
                    int pos = 1;
                    do {
                        if(pathName.Substring(pos - 1, 3) == "..\\") {
                            startPath = AddDirSep(System.IO.Path.GetDirectoryName(startPath));
                            pos += "..\\".Length;
                        } else
                            pos += 1;

                        if(pos >= pathName.Length) break;
                    } while(true);

                    pathName = pathName.Replace("..\\", "");
                    return System.IO.Path.Combine(startPath, pathName);
                } else
                    return pathName;
            }
        } //CalculatePath function

        public static string AddDirSep(string pathName)
        {
            if(string.IsNullOrEmpty(pathName)) return "";
            string results = pathName.Trim();
            if(results.Length == 0) return "";

            char seperator = System.IO.Path.DirectorySeparatorChar;
            if(results.IndexOf(System.IO.Path.AltDirectorySeparatorChar) < 0) seperator = System.IO.Path.AltDirectorySeparatorChar;

            if(!results.EndsWith(seperator.ToString())) results += seperator;

            return results;
        } //AddDirSep function

        //Get the path portion of a filename
        public static string GetPathName(string fileName)
        {
            return System.IO.Path.GetDirectoryName(fileName);
        } //GetPathName

        public static string GetFileExtension(string fileName)
        {
            string ext = System.IO.Path.GetExtension(fileName);
            if(ext.StartsWith("."))
                return ext.Substring(1);
            else
                return ext;
        } //GetFileExtension

        public static string FindIncludeFile(string originalFileName, string scriptPath, string libraryPath)
        {
            string strFileName = CalculatePath(originalFileName, scriptPath);

            if(!System.IO.File.Exists(strFileName)) {
                strFileName = CalculatePath(originalFileName, libraryPath);
                if(!System.IO.File.Exists(strFileName)) strFileName = "";
            }

            return strFileName;
        } //FindIncludeFile

#region "FILE STUFF"
        //public static Function File_ReadLong(ByVal FileHandle As int) As int
        //    Dim dwTemp As int
        //    FileGet(FileHandle, dwTemp)
        //    return dwTemp
        //} //

        //public static Function File_ReadBoolean(ByVal FileHandle As int) As bool
        //    Dim aTemp As Byte
        //    FileGet(FileHandle, aTemp)
        //    return IIf(aTemp = 0, false, true)
        //} //

        //public static Function File_ReadString(ByVal FileHandle As int) as string
        //    Dim dwTemp As int
        //    FileGet(FileHandle, dwTemp)
        //    string strTemp = Space(dwTemp);
        //    FileGet(FileHandle, strTemp)
        //    return strTemp
        //} //

        //public static void File_SaveLong(ByVal FileHandle As int, ByVal newValue As int)
        //    Dim dwTemp As int = newValue
        //    FilePut(FileHandle, dwTemp)
        //} //

        //public static void File_SaveBoolean(ByVal FileHandle As int, ByVal newValue As bool)
        //    Dim aTemp As Byte = IIf(!newValue, 0, 1)
        //    FilePut(FileHandle, aTemp)
        //} //

        //public static void File_SaveString(ByVal FileHandle As int, ByVal newValue as string)
        //    string strTemp = newValue;
        //    Dim dwTemp As int = Len(strTemp)
        //    FilePut(FileHandle, dwTemp)
        //    FilePut(FileHandle, strTemp)
        //} //

        //// public static void File_ReadHeader(ByVal FileHandle As int, ByVal HeaderSize As int, ByRef Header as string, ByRef Version As int)
        //public static void File_ReadHeader(ByRef ioFile As System.IO.FileStream, ByVal HeaderSize As int, ByRef Header as string, ByRef Version As int)
        //    Dim binary = new System.IO.BinaryReader(ioFile)
        //    string temp = Space(HeaderSize);
        //    temp = binary.ReadString(4) 'FileGet(FileHandle, strTemp)
        //    // Header = strTemp
        //
        //    // Dim dwTemp As int
        //    // FileGet(FileHandle, dwTemp)
        //    // Version = dwTemp
        //} //

        //public static void File_SaveHeader(ByVal FileHandle As int, ByVal Header as string, ByVal Version As int)
        //    string strTemp = Header;
        //    FilePut(FileHandle, strTemp)
        //
        //    Dim dwTemp As int = Version
        //    FilePut(FileHandle, dwTemp)
        //} //

        public static string File_ReadFixedString(System.IO.BinaryReader ioFile, int fixedLength)
        {
            try {
                byte[] bytes = new byte[fixedLength];
                ioFile.Read(bytes, 0, fixedLength);
                return System.Text.Encoding.ASCII.GetString(bytes, 0, fixedLength);
            } catch {
                return "";
            }
        } //File_ReadFixedString

        public static bool File_WriteFixedString(System.IO.BinaryWriter ioFile, string  value)
        {
            if(value.Length == 0) return true;
            try {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(value);
                ioFile.Write(bytes, 0, bytes.Length);
                return true;
            } catch {
                return false;
            }
        } //File_WriteFixedString

        public static void File_ReadHeader(System.IO.BinaryReader data, int fixedHeaderSize, out string outFileHeader, out int outFileVersion)
        {
            outFileHeader = File_ReadFixedString(data, fixedHeaderSize);
            outFileVersion = data.ReadInt32();
        } //File_ReadHeader

        public static void File_WriteHeader(System.IO.BinaryWriter data, string fileHeader, int fileVersion)
        {
            File_WriteFixedString(data, fileHeader);
            data.Write(fileVersion);
        } //File_WriteHeader

        public static string File_ReadString(System.IO.BinaryReader data)
        {
            int stringSize = data.ReadInt32();
            return File_ReadFixedString(data, stringSize);
        } //File_ReadHeader

        public static void File_WriteString(System.IO.BinaryWriter data, string value)
        {
            data.Write(value.Length);
            File_WriteFixedString(data, value);
        } //File_WriteString
#endregion
    } //FileManager

    public static class StringManager
    {
        public static int FindStringOutside(string check, int startAt, string toFind, int endAt = 0, string skipTrailingChar = "", bool passByQuotes = false)
        {
            if(string.IsNullOrEmpty(check)) return 0;
            if (!check.ToLower().Contains(toFind.ToLower())) return 0;

            int dwPos = startAt;
            int dwEnd = endAt;
            if(dwPos < 1) dwPos = 1;
            if(dwEnd < 1 || dwEnd > check.Length) dwEnd = check.Length;

            string cur;
            int    insides = 0;
            bool   inQuote = false;

            bool foundNonTrailingChar = (skipTrailingChar.Length == 0);

            do {
                cur = check.Substring(dwPos - 1, toFind.Length);

                if(!foundNonTrailingChar && cur.StartsWith(skipTrailingChar)) {
                    dwPos += 1;
                } else {
                    if(!foundNonTrailingChar) foundNonTrailingChar = true;

                    if(string.Equals(cur, toFind) && insides == 0 && !inQuote) {
                        return dwPos;
                    } else {
                        if(cur.StartsWith("\"")) {
                            if(passByQuotes) inQuote = !inQuote;
                        } else if(cur.StartsWith("(")) {
                            insides += 1;
                        } else if(cur.StartsWith(")")) {
                            insides -= 1;
                            if(insides < 0) return 0;
                        }

                        dwPos += 1;
                    }
                }

                if(dwPos + (toFind.Length - 1) > dwEnd) break;
            } while(true);

            return 0;
        } //FindStringOutside

        public static int FindStringOutsideRev(string check, int startAt, string toFind, int endAt = 0, string skipTrailingChar = "", bool passByQuotes = false)
        {
            if(string.IsNullOrEmpty(check) || !check.ToLower().Contains(toFind.ToLower())) return 0;

            int dwPos = startAt;
            int dwEnd = endAt;
            if(dwPos < 1) dwPos = 1;
            if(dwPos > check.Length) dwPos = check.Length;
            if(dwEnd < 1 || dwEnd > check.Length) dwEnd = 1;

            string cur = "";
            int    insides = 0;
            bool   inQuote = false;

            bool foundNonTrailingChar = string.IsNullOrEmpty(skipTrailingChar);

            do {
                cur = check.Substring(dwPos - 1, toFind.Length);

                if(!foundNonTrailingChar && cur.StartsWith(skipTrailingChar)) {
                    dwPos -= 1;
                } else {
                    if(!foundNonTrailingChar) foundNonTrailingChar = true;

                    if(string.Equals(cur, toFind) && insides == 0 && !inQuote) {
                        return dwPos;
                    } else {
                        if(cur.StartsWith("\"")) {
                            if(passByQuotes) inQuote = !inQuote;
                        } else if(cur.StartsWith("(")) {
                            insides += 1;
                        } else if(cur.StartsWith(")")) {
                            insides -= 1;
                            if(insides < 0) return 0;
                        }

                        dwPos -= 1;
                    }
                }

                if(dwPos < dwEnd) break;
            } while(true);

            return 0;
        } //FindStringOutsideRev

        public static List<string> GetArguments(string Args, bool trimArguments = true)
        {
            // Split the arguments.
            List<string> argumentsList; SmartSplit(Args, ",", out argumentsList);
            if(argumentsList == null || argumentsList.Count == 0) return null;

            if(trimArguments) {
                for(int index = 0; index < argumentsList.Count; index++) {
                    argumentsList[index] = argumentsList[index].Trim();
                } // dwArg
            }

            return argumentsList;
        } //GetArguments

        public static bool IsQuoteString(string text)
        {
            if(text.StartsWith("\"") && text.EndsWith("\""))
                return true;
            else if(text.StartsWith("'") && text.EndsWith("'"))
                return true;
            else
                return false;
        } //IsQuoteString

        public static string GetQuoteString(string text)
        {
            if(IsQuoteString(text))
                return text.Substring(1, text.Length - 2);
            else
                return text;
        } //GetQuoteString

        public static string RemoveDoubleSpaces(string text)
        {
            string strTemp = text;

            string strLeft = "", strRight = "";
            do {
                int dwPos = FindStringOutside(strTemp, 1, "  ");
                if(dwPos == 0) break;

                strLeft = strTemp.Substring(0, dwPos - 1);
                strRight = strTemp.Substring((dwPos - 1) + "  ".Length);
                strTemp = strLeft + strRight;
            } while(true);

            return strTemp;
        } //RemoveDoubleSpaces

        public static string RemoveTabs(string text)
        {
            // if(the string is empty then ... exit this procedure.
            if(string.IsNullOrEmpty(text)) return "";

            string results = "";
            bool inQuote = false;

            // Go through each character in the string ...
            for(int index = 0; index < text.Length; index++) {
                string strChar = text.Substring(index, 1);

                if(strChar.Equals("\t") && !inQuote) {
                    results += "";
                } else {
                    if(strChar.Equals("\"")) inQuote = !inQuote;
                    results += strChar;
                }
            } // dwPos

            return results;
        } //RemoveTabs

        public static string FixLine(string line) { return RemoveDoubleSpaces(RemoveTabs(line.Trim())); }

        public static void SmartSplit(string text, string delimiter, out List<string> outSplit, int limit = -1, bool checkIfIsInQuotes = true, bool checkForParanthese = true, bool checkForBrackets = true)
        {
            outSplit = new List<string>();
            if(string.IsNullOrEmpty(text)) return;

            int  insides = 0,     insides2 = 0;
            bool inQuote = false, inQoute2 = false;

            outSplit.Add("");

            string strChar;
            for(int index = 0; index < text.Length; index++) {
                strChar = text.Substring(index, delimiter.Length);

                if(insides == 0 && insides2 == 0 && !inQuote && !inQoute2 && string.Equals(strChar, delimiter)) {
                    if(limit >= 0 && outSplit.Count >= limit) return;
                    outSplit.Add("");
                } else {
                    if(checkForParanthese && string.Equals(strChar, "(")) insides  += 1;
                    if(checkForParanthese && string.Equals(strChar, ")")) insides  -= 1;
                    if(checkForBrackets && string.Equals(strChar, "["))   insides2 += 1;
                    if(checkForBrackets && string.Equals(strChar, "]"))   insides2 -= 1;
                    if(checkIfIsInQuotes && string.Equals(strChar, "\"")) inQuote   = !inQuote;
                    if(checkIfIsInQuotes && string.Equals(strChar, "'"))  inQoute2  = !inQoute2;

                    outSplit[outSplit.Count - 1] += strChar;
                }
            } // dwPos
        } //SmartSplit

        public static bool HasCommentOnLine(string line, out string outLine, out string outComment, string commentChar = "#")
        {
            // Set defaults for returns.
            outLine    = line;
            outComment = "";

            // Find the comment string
            int pos = FindStringOutside(line, 1, commentChar, 0, "", true);

            if(pos > 0) {
                outLine    = line.Substring(0, pos - 1).Trim();
                outComment = line.Substring(pos).Trim();
                return true;
            } else {
                return false;
            }
        } //HasCommentOnLine

        public static void SplitDataToLines(string data, out List<string> outLines)
        {
            outLines = new List<string>();
            if(string.IsNullOrEmpty(data)) return;

            int pos = 0;
            do {
                pos = data.IndexOf(System.Environment.NewLine);
                if(pos < 0) return;

                outLines.Add(data.Substring(0, pos));
                
                data = data.Substring(pos + System.Environment.NewLine.Length);
            } while(true);
        } //SplitDataToLines

        public static string ArrayToString(List<string> items, int start = 1, bool addQuotes = false)
        {
            string results = "";

            if(items != null && items.Count > 0) {
                for(int index = start; index <= (items.Count - (1 - start)); index++) {
                    results = (string.IsNullOrEmpty(results) ? "" : results + ", ") + (addQuotes ? "\"" : "") + items[index - 1] + (addQuotes ? "\"" : "");
                } // dwIndex
            }

            return results;
        } //ArrayToString

        // Name:          IsInsideParenthesis
        // Version:       Build 1
        // Creation Date: Saturday, July 1st, 2006 - 6:00pm
        // Description:   Returns if a specified position in a string is inside a
        //                parenthesis left and right characters '(', ')'.
        //                This is useful for parsing math strings.
        // Inputs:
        //   Str          - The source string to be tested against.
        //   Position     - Where to start testing in the source string.
        //   LeftMark     - A custom left parenthesis character.
        //   RightMark    - A custom right parenthesis character.
        // Outputs:
        //   Default      - Returns true or false if the test worked.
        //   outLeftPos   - Returns the left parenthesis character's position.
        //   outRightPos  - Returns the right parenthesis character's position.
        //
        // Update Details:
        //  -- Build 1 - Saturday, July 1st, 2006 --
        //     * (6:00pm) Created.
		public static bool IsInsideParenthesis(string str, int position, out int outLeftPos, out int outRightPos, string leftMark = "(", string rightMark = ")")
        {
			// Set default return positions.
			outLeftPos  = 0;
			outRightPos = 0;

			// Prepare the inside counter.
			int inside = 0;

			// Determine the character size to use in the loops.
			int charSize = leftMark.Length;
			if(rightMark.Length > charSize) charSize = rightMark.Length;

			string chr;
			for(int index = position; index >= 1; index--) {
				chr = str.Substring(index - 1, charSize);

				if(inside == 0 && string.Equals(chr, leftMark)) {
					outLeftPos = index;
					break;
				} else if(string.Equals(chr, rightMark)) {
					inside += 1;
				} else if(string.Equals(chr, leftMark)) {
					inside -= 1;
				}
			} //position

			// if(the left parenthesis character was not found then ... exit this procedure.
			if(outLeftPos < 1) return false;

			// Reset the inside counter.
			inside = 0;

			for(int index = position; index <= str.Length; index++) {
                chr = str.Substring(index - 1, charSize);

				if(inside == 0 && string.Equals(chr, rightMark)) {
					outRightPos = index;
					break;
				} else if(string.Equals(chr, leftMark)) {
					inside += 1;
				} else if(string.Equals(chr, rightMark)) {
					inside -= 1;
				}
			} //position

			// if(the right parenthesis character was not found then ... exit this procedure.
			if(outRightPos < 1) return false;

			// return successful.
			return true;
		} //IsInsideParenthesis

        public static string FixSpecialStrings(string inString)
        {
            switch(inString.ToLower()) {
                case("true"):     return "1";
                case("false"):    return "0";
                case("notempty"): return "#true#";
                case("empty"):    return "#false#";
                default:
                    if(inString.StartsWith("0x", System.StringComparison.CurrentCultureIgnoreCase)) {
                        inString = inString.Substring(2);
                        //return System.Convert.ToChar(string.Format("{0:X}", inString)).ToString();
                        return System.Convert.ToChar(System.Convert.ToInt32(inString.ToUpper(), 16)).ToString();
                    } else {
                        return inString;
                    }
            }
        } //FixSpecialStrings
    } //StringManager
} // DSSL namespace
