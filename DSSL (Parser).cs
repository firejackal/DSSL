#region "VERISON HISTORY"
/*
' File:        DSSL - Parser.bas
' Name:        DSSL Parser
' Author:      Michael Smyer
' Version:     13
' Description: Parses and converts text-based script files into
'              DSSL event code.
'
' Changes:
'   Version 13 - Tuesday, April 21st, 2009
'     * (19:34) Update: Updated ParseFromFile() with the changes to script framework and now returns
'       the script instead of a boolean, when failed it will return nothing.
'     * (19:44) Update: Updated ParseFromData() with the changes to script framework and now returns
'       the script instead of a boolean, when failed it will return nothing.
'     * (19:45) Update: Cleaned up CheckCompilerSettingsFromFile().
'     * (19:46) Update: Cleaned up CheckCompilerSettingsFromData().
'     * (19:49) Update: Cleaned up CheckCompilerSettingsFromLines().
'     * (19:55) Update: Updated CheckParameterValue() with changes to the script framework.
'     * (19:56) Update: Updated ParseLine (script version) with changes to the script framework.
'     * (20:07) Update: Updated ParseLine (events version) with changes to the script framework.
'
'   --Version 12 - Wednesday, April 30th, 2008--
'     * (15:11) Updated all the code to VB.net.
'
'   --Version 11 - Tuesday, July 31st, 2007--
'     * (19:00) Updated the 'DSSL_Parse_RapidCode_IsVarSetLine()'
'       procedure, removed the variable '&&' and '&' removal for the
'       left side. This was causing problems for the new linker
'       application.
'
'   --Version 10 - Sunday, June 29th, 2007--
'     * (21:13) in the procedure 'DSSL_Parse_RapidCode_IsVarSetLine()'
'       removed the variable '&&' and '&' removal for the right side,
'       this was causing problems setting a variable to another
'       variable's value.
'
'   --Version 9 - Sunday, June 25th, 2007--
'     * (4:14)  Added it so now when parsing the else if types, it
'       will check for 'ElseIf' and 'Else If'.
'
'   --Version 8 - Sunday, June 17th, 2007
'     * (11:59) Added the ability to include source files.
'
'   --Version 7 - Saturday, June 16th, 2007--
'     * (11:41) Updated the procedure 'GetArguments()' and added it so
'       now the arguments can be trimmed of any leading spaces.
'     * (11:41) Now the function return, and variable set can accept
'       multiple variables.
'
'   --Version 6 - Monday, July 17th, 2006--
'     * (5:05pm) Now functions support accessibility types, being
'       either public, private, or global.
'
'   --Version 5 - Sunday, July 16th, 2006--
'     * (7:53pm) Global function support broke dynamic strings, so I
'       added a couple of rule checking to check if it's a dynamic
'       string variable (by checking for the '&' sign).
'
'   --Version 4 - Saturday, July 15th, 2006--
'     * (9:39am) Added the procedure 'DSSL_Parse_RapidCode_LineEx()'
'       to allow direct input for the events and pointers data
'       instead of script data structure.
'     * (10:38am) Added the support for global functions, meaning now
'       an external function doesn't require the module and procedure
'       character '.' seperator.
'     * (1:05pm) Added support to load a script file from a lines
'       string array.
'
'   --Version 3 - Sunday, July 9th, 2006--
'     * (8:13am) Removed the comment event line detection and now is
'       using a new method that allows a comment to even be on a
'       line! The parser will automaticly add the comment before the
'       actual event on the line.
'
'   --Version 2 - Saturday, July 8th, 2006--
'     * (8:47am) Added compiler settings to the script files, these
'       are file header, compiler version required, and script ID.
'     * (8:46pm) Fixed some mistypes.
'     * (9:22pm) Fixed some errors with external functions not being
'       called correctly with their arguments.
 */
#endregion

using System.Collections.Generic;

namespace DSSL
{
    public static class RapidCodeParser //: Parser
    {
#region "Constants"
        private const string RapidCodeHeader         = "DSSL Rapid Code Script";
        private const int    RapidCodeVersionMinimum = 1;
        private const int    RapidCodeVersionCurrent = 1;

        private const string FunctionArgumentsStart  = "(";
        private const string FunctionArgumentsEnd    = ")";
        private const string SYNTAX_STOP_1           = "Stop";
        private const string SYNTAX_STOP_2           = "Stop" + FunctionArgumentsStart + FunctionArgumentsEnd;
        private const string SYNTAX_INCLUDE_START    = "Include" + FunctionArgumentsStart;
        private const string SYNTAX_INCLUDE_END      = FunctionArgumentsEnd;
        private const string SYNTAX_MARK_START       = "Mark" + FunctionArgumentsStart;
        private const string SYNTAX_MARK_END         = FunctionArgumentsEnd;
        private const string SYNTAX_GOTO_START       = "Goto" + FunctionArgumentsStart;
        private const string SYNTAX_GOTO_END         = FunctionArgumentsEnd;
        private const string SYNTAX_DO_START         = "Do" + FunctionArgumentsStart;
        private const string SYNTAX_DO_END           = FunctionArgumentsEnd;
        private const string SYNTAX_IF_START         = "If ";
        private const string SYNTAX_IF_STARTEND      = ") {";
        private const string SYNTAX_IF_ELSE          = "Else";
        private const string SYNTAX_IF_END           = "End If";
        private const string SYNTAX_PUBLIC           = "Public";
        private const string SYNTAX_PRIVATE          = "Private";
        private const string SYNTAX_GLOBAL           = "Global";
        private const string SYNTAX_FUNCTION_START   = "Function ";
        private const string SYNTAX_FUNCTION_END     = "End Function";
        private const string SYNTAX_DECISION_START   = "Decision ";
        private const string SYNTAX_DECISION_ITEM    = "Decide ";
        private const string SYNTAX_DECISION_IS      = "Is ";
        private const string SYNTAX_DECISION_DEFAULT = "Decide Default";
        private const string SYNTAX_DECISION_END     = "End Decision";
        private const string SYNTAX_LOOP_START       = "Do";
        private const string SYNTAX_LOOP_BREAK       = "Break";
        private const string SYNTAX_LOOP_END         = "Loop";
        private const string SYNTAX_DELETE_START     = "Delete" + FunctionArgumentsStart;
        private const string SyntaxVariableAddStart  = "Add" + FunctionArgumentsStart;
        private const string SyntaxVariableAddEnd    = FunctionArgumentsEnd;
        private const string SyntaxVariableGetStart  = "Get" + FunctionArgumentsStart;
        private const string SyntaxArgumentSeparator = ",";
        private const string VARIABLE_OPERATOR_VALUE = "&&";

        private const string PARSER_TRUE             = "#true#";
#endregion //Constants

        private static string Right(string original, int numberCharacters)
        {
            return original.Substring(numberCharacters > original.Length ? 0 : original.Length - numberCharacters);
        } //Right

#region "Syntax"
        private static bool IsStopLine(string line)
        {
            return (string.Equals(line, SYNTAX_STOP_1, System.StringComparison.CurrentCultureIgnoreCase) || string.Equals(line, SYNTAX_STOP_2, System.StringComparison.CurrentCultureIgnoreCase));
        } //IsStopLine function

        // Statement: Include(name)
        // Rules:
        //  * Begins with 'Include(' and ends with ')'.
        //  * Contents are the file name.
        private static bool IsIncludeLine(string line, out string outName)
        {
            outName = "";

            if(line.StartsWith(SYNTAX_INCLUDE_START, System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(SYNTAX_INCLUDE_END, System.StringComparison.CurrentCultureIgnoreCase)) {
                outName = Right(line, line.Length - SYNTAX_INCLUDE_START.Length);
                outName = outName.Substring(0, outName.Length - 1);
                outName = StringManager.GetQuoteString(outName);
                return true;
            } else
                return false;
        } //IsIncludeLine function

        // Statement: Mark(name)
        // Rules:
        //  * Begins with 'Mark(' and ends with ')'.
        //  * Contents are the name.
        private static bool IsBookMarkSetLine(string line, out string outName)
        {
            outName = "";

            if(line.StartsWith(SYNTAX_MARK_START, System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(SYNTAX_MARK_END, System.StringComparison.CurrentCultureIgnoreCase)) {
                outName = Right(line, line.Length - SYNTAX_MARK_START.Length);
                outName = outName.Substring(0, outName.Length - 1);
                outName = StringManager.GetQuoteString(outName);
                return true;
            } else
                return false;
        } //IsBookMarkSetLine function

        // Statement: Goto(name)
        // Rules:
        //  * Begins with 'Goto(' and ends with ')'.
        //  * Contents are the name.
        private static bool IsBookMarkGotoLine(string line, out string outName)
        {
            outName = "";

            if(line.StartsWith(SYNTAX_GOTO_START, System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(SYNTAX_GOTO_END, System.StringComparison.CurrentCultureIgnoreCase)) {
                outName = Right(line, line.Length - SYNTAX_GOTO_START.Length);
                outName = outName.Substring(0, outName.Length - 1);
                outName = StringManager.GetQuoteString(outName);
                return true;
            } else
                return false;
        } //IsBookMarkGotoLine function

        // Statement: if(xxx = xxx) {
        // Rules:
        //   * Starts with 'If // and ends with ') {'.
        //   * Contains a left expression and a right expression seperated
        //     by an operator (=, >, <, !=, =>, =<).
        private static bool IsIfStartLine(string line, out Parser.Comparisons outOperator, out string outLeft, out string outRight)
        {
            outOperator = 0;
            outLeft     = "";
            outRight    = "";

            if(line.StartsWith(SYNTAX_IF_START, System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(SYNTAX_IF_STARTEND, System.StringComparison.CurrentCultureIgnoreCase)) {
                string strTemp = Right(line, line.Length - SYNTAX_IF_START.Length).Trim();
                strTemp = strTemp.Substring(0, strTemp.Length - SYNTAX_IF_STARTEND.Length).Trim();

                int opPos, opSize;
                Parser.Comparisons eOperator = Parser.Translator.ComparisonToValue(strTemp, out opPos, out opSize);

                string strLeft, strRight;
                if(opSize == 0) {
                    eOperator = Parser.Comparisons.Equals;
                    strLeft = strTemp.Trim();
                    strRight = PARSER_TRUE;
                } else {
                    strLeft = strTemp.Substring(0, opPos - 1).Trim();
                    strRight = strTemp.Substring(opPos + opSize).Trim();
                }

                if(!string.IsNullOrEmpty(strLeft) && !string.IsNullOrEmpty(strRight)) {
                    outOperator = eOperator;
                    outLeft     = strLeft;
                    outRight    = strRight;
                    return true;
                }
            }

            return false;
        } //IsIfStartLine

        // Statement: } else if(xxx = xxx) {
        // Rules:
        //   * Starts with 'ElseIf // and ends with ') {'.
        //   * Contains a left expression and a right expression seperated
        //     by an operator (=, >, <, !=, =>, =<).
        private static bool IsIfElseIfLine(string line, out Parser.Comparisons outOperator, out string outLeft, out string outRight)
        {
            outOperator = 0;
            outLeft     = "";
            outRight    = "";

            string tempString = "";
            if((line.StartsWith("ElseIf ", System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(SYNTAX_IF_STARTEND, System.StringComparison.CurrentCultureIgnoreCase))) {
                tempString = Right(line, line.Length - "elseif ".Length).Trim();
                tempString = tempString.Substring(0, tempString.Length - SYNTAX_IF_STARTEND.Length).Trim();
            } else if((line.StartsWith("Else if(", System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(SYNTAX_IF_STARTEND, System.StringComparison.CurrentCultureIgnoreCase))) {
                tempString = Right(line, line.Length - "else if ".Length).Trim();
                tempString = tempString.Substring(0, tempString.Length - SYNTAX_IF_STARTEND.Length).Trim();
            } else 
                return false;

            int opPos = 0, opSize = 0;
            Parser.Comparisons op = Parser.Translator.ComparisonToValue(tempString, out opPos, out opSize);
            string leftString = "", rightString = "";

            if(opSize == 0) {
                op = Parser.Comparisons.Equals;
                leftString  = tempString.Trim();
                rightString = PARSER_TRUE;
            } else {
                leftString  = tempString.Substring(0, opPos - 1).Trim();
                rightString = tempString.Substring(opPos + opSize).Trim();
            }

            if(!string.IsNullOrEmpty(leftString) && !string.IsNullOrEmpty(rightString)) {
                outOperator = op;
                outLeft     = leftString;
                outRight    = rightString;
                return true;
            } else 
                return false;
        } //IsIfElseIfLine

        private static bool IsIfElseLine(string line)
        {
            return string.Equals(line, SYNTAX_IF_ELSE, System.StringComparison.CurrentCultureIgnoreCase);
        } //IsIfElseLine function

        private static bool IsIfEndLine(string line)
        {
            return string.Equals(line, SYNTAX_IF_END, System.StringComparison.CurrentCultureIgnoreCase);
        } //IsIfEndLine function

        // Function Main(Arg1, Arg2, ...)
        private static bool IsFunctionStartLine(string line, out string outFunctionName, out string outFunctionArgs, out Parser.FunctionAccess outFunctionAccess)
        {
            // Bu default return false.
            outFunctionName   = "";
            outFunctionArgs   = "";
            outFunctionAccess = Parser.FunctionAccess.Global;

            bool isFunction = false;
            bool autoAccess = false;

            if(line.StartsWith(SYNTAX_PUBLIC + " " + SYNTAX_FUNCTION_START, System.StringComparison.CurrentCultureIgnoreCase)) {
                isFunction = true;
                outFunctionAccess = Parser.FunctionAccess.Public;
                line = line.Substring((SYNTAX_PUBLIC + " " + SYNTAX_FUNCTION_START).Length).Trim();
            } else if(line.StartsWith(SYNTAX_PRIVATE + " " + SYNTAX_FUNCTION_START, System.StringComparison.CurrentCultureIgnoreCase)) {
                isFunction = true;
                outFunctionAccess = Parser.FunctionAccess.Private;
                line = line.Substring((SYNTAX_PRIVATE + " " + SYNTAX_FUNCTION_START).Length).Trim();
            } else if(line.StartsWith(SYNTAX_GLOBAL + " " + SYNTAX_FUNCTION_START, System.StringComparison.CurrentCultureIgnoreCase)) {
                isFunction = true;
                outFunctionAccess = Parser.FunctionAccess.Global;
                line = line.Substring((SYNTAX_GLOBAL + " " + SYNTAX_FUNCTION_START).Length).Trim();
            } else if(line.StartsWith(SYNTAX_FUNCTION_START, System.StringComparison.CurrentCultureIgnoreCase)) {
                isFunction = true;
                outFunctionAccess = Parser.FunctionAccess.Public;
                autoAccess = true;
                line = line.Substring(SYNTAX_FUNCTION_START.Length).Trim();
            }

            if(isFunction) {
                int startPos = StringManager.FindStringOutside(line, 1, "(");

                if(startPos > 0) {
                    int endPos = StringManager.FindStringOutside(line, startPos + 1, ")");

                    if(endPos > 0) {
                        // return the function name.
                        outFunctionName = line.Substring(0, startPos - 1).Trim();
                        if(autoAccess) {
                            if(string.Equals(outFunctionName, "main", System.StringComparison.CurrentCultureIgnoreCase)) {
                                outFunctionAccess = Parser.FunctionAccess.Global;
                            }
                        }

                        // return the arguments.
                        outFunctionArgs = line.Substring(startPos, (endPos - startPos) - 1).Trim();
                        return true;
                    }
                }
            }

            return false;
        } //IsFunctionStartLine function

        private static bool IsFunctionEndLine(string line)
        {
            return line.Equals(SYNTAX_FUNCTION_END, System.StringComparison.CurrentCultureIgnoreCase);
        } //IsFunctionEndLine

        private static bool IsFunctionReturnLine(string line, out string outReturnValue)
        {
            outReturnValue = "";

            if(line.StartsWith("return(", System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(")", System.StringComparison.CurrentCultureIgnoreCase)) {
                outReturnValue = line.Substring("return(".Length);
                outReturnValue = outReturnValue.Substring(0, outReturnValue.Length - 1);
                return true;
            } else
                return false;
        } //IsFunctionReturnLine

        // Statement: Call(name, args, ...)
        // Rules:
        //   * Begins with 'Call(' and ends with ')'.
        //   * Contents are arguments, first is the function's name, and
        //     following are the function's arguments.
        private static bool IsFunctionCallLine(string line, out string outFunctionName, out string outFunctionArgs)
        {
            outFunctionName = "";
            outFunctionArgs = "";

            if(line.StartsWith("call(", System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(")", System.StringComparison.CurrentCultureIgnoreCase)) {
                string strTemp = Right(line, line.Length - "Call(".Length);
                strTemp = strTemp.Substring(0, strTemp.Length - 1);
                strTemp = strTemp.Trim();

                int dwPos = StringManager.FindStringOutside(strTemp, 1, ",");

                if(dwPos > 0) {
                    outFunctionName = StringManager.GetQuoteString(strTemp);
                } else {
                    outFunctionName = StringManager.GetQuoteString(strTemp.Substring(0, dwPos - 1).Trim());
                    //retFunctionArgs = FixArguments(Mid$(strTemp, 1 + dwPos).Trim());
                    outFunctionArgs = strTemp.Substring(dwPos).Trim();
                }
                return true;
            } else
                return false;
        } //IsFunctionCallLine

        // Statement: Decision xxx
        // Rules:
        //   * Starts with 'Decision '.
        //   * Ends with an expression.
        private static bool IsDecisionStartLine(string line, out string outExpression)
        {
            outExpression = "";

            if(line.StartsWith(SYNTAX_DECISION_START, System.StringComparison.CurrentCultureIgnoreCase)) {
                outExpression = Right(line, line.Length - SYNTAX_DECISION_START.Length).Trim();
                return true;
            } else
                return false;
        } //IsDecisionStartLine

        // Statement: Decide xxx
        // Rules:
        //   * Starts with 'Decide '.
        //   * Ends with an expression.
        private static bool IsDecisionItemLine(string line, out string outExpression, out Parser.Comparisons outComparison)
        {
            outExpression = "";
            outComparison = Parser.Comparisons.Equals;

            if(line.StartsWith(SYNTAX_DECISION_ITEM, System.StringComparison.CurrentCultureIgnoreCase)) {
                string strTemp = Right(line, line.Length - SYNTAX_DECISION_ITEM.Length).Trim();

                if(strTemp.StartsWith(SYNTAX_DECISION_IS, System.StringComparison.CurrentCultureIgnoreCase)) {
                    strTemp = Right(line, line.Length - SYNTAX_DECISION_IS.Length).Trim();
                    int dwPos = 0, dwSize = 0;
                    outComparison = Parser.Translator.ComparisonToValue(strTemp, out dwPos, out dwSize);

                    if(dwSize == 0) {
                        outExpression = StringManager.GetQuoteString(strTemp);
                    } else {
                        strTemp = Right(line, line.Length - dwSize).Trim();
                        outExpression = StringManager.GetQuoteString(strTemp);
                    }
                } else {
                    outExpression = StringManager.GetQuoteString(strTemp);
                }

                return true;
            } else
                return false;
        } //IsDecisionItemLine

        // Statement: Decide Default
        // Rules:
        //   * Must contain 'Decide Default'
        //   * Must be used before other decision syntax parsing statments.
        private static bool IsDecisionDefaultLine(string line)
        {
            return line.Equals(SYNTAX_DECISION_DEFAULT, System.StringComparison.CurrentCultureIgnoreCase);
        } //IsDecisionDefaultLine

        // Statement: End Decision
        // Rules:
        //   * Contains all the words "End Decision".
        private static bool IsDecisionEndLine(string line)
        {
            return line.Equals(SYNTAX_DECISION_END, System.StringComparison.CurrentCultureIgnoreCase);
        } //IsDecisionEndLine

        private static bool IsDoStatement(string line, out string outExpression)
        {
            outExpression = "";

            if(line.StartsWith(SYNTAX_DO_START, System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(SYNTAX_DO_END, System.StringComparison.CurrentCultureIgnoreCase)) {
                outExpression = line.Substring(SYNTAX_DO_START.Length);
                outExpression = line.Substring(0, outExpression.Length - 1);
                outExpression = StringManager.GetQuoteString(outExpression);
                return true;
            } else
                return false;
        } //IsDoStatement

        private static bool IsLoopStartLine(string line)
        {
            return line.Equals(SYNTAX_LOOP_START, System.StringComparison.CurrentCultureIgnoreCase);
        } //IsLoopStartLine

        private static bool IsLoopBreakLine(string line)
        {
            return line.Equals(SYNTAX_LOOP_BREAK, System.StringComparison.CurrentCultureIgnoreCase);
        } //IsLoopBreakLine

        private static bool IsLoopEndLine(string line)
        {
            return line.Equals(SYNTAX_LOOP_END, System.StringComparison.CurrentCultureIgnoreCase);
        } //IsLoopEndLine

        // Add(name, value, [scope])
        // Add(name[max], [value, value, ...], [scope])
        // Rules:
        //  * A single function line.
        private static bool IsVarAddLine(string line, out string outVariableName, out string outVariableValue, out Parser.VariableScopes outVariableScope)
        {
            outVariableName = "";
            outVariableValue = "";
            outVariableScope = Parser.VariableScopes.Private;

            if(line.StartsWith(SyntaxVariableAddStart, System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(SyntaxVariableAddEnd, System.StringComparison.CurrentCultureIgnoreCase)) {
                string variablesString = line.Substring(SyntaxVariableAddStart.Length);
                variablesString = StringManager.GetQuoteString(variablesString.Substring(0, variablesString.Length - 1));

                List<string> variablesList; StringManager.SmartSplit(variablesString, SyntaxArgumentSeparator, out variablesList);

                if(variablesList.Count == 2) {
                    outVariableName = StringManager.GetQuoteString(variablesList[0]);
                    outVariableValue = StringManager.GetQuoteString(variablesList[1]);
                    outVariableScope = Parser.VariableScopes.Private;
                    return true;
                } else if(variablesList.Count == 3) {
                    outVariableName = StringManager.GetQuoteString(variablesList[0]);
                    outVariableValue = StringManager.GetQuoteString(variablesList[1]);
                    outVariableScope = Parser.VariableScopes.Private;
                    switch(StringManager.GetQuoteString(variablesList[2]).ToLower()) {
                        case("global"):  outVariableScope = Parser.VariableScopes.Global; break;
                        case("public"):  outVariableScope = Parser.VariableScopes.Public; break;
                        case("private"):
                        case("local"):   outVariableScope = Parser.VariableScopes.Private; break;
                    }
                    return true;
                }
            }

            return false;
        } //IsVarAddLine

        // &&xxx[index] = value or &&yyy[index]
        // Rules:
        //  * Has one '=' between outside strings.
        //  * Left value must contain more then one variables defined by the '&&' characters.
        private static bool IsVarSetLine(string line, out string outVariableNames, out string outVariableValues)
        {
            outVariableNames = "";
            outVariableValues = "";

            int dwEqualsSign = StringManager.FindStringOutside(line, 1, "=");

            if(dwEqualsSign > 0) {
                string strLeft = line.Substring(0, dwEqualsSign - 1).Trim();
                string strRight = line.Substring(dwEqualsSign).Trim();

                bool goodLeft = false;
                string newLeft = "";
                if(!string.IsNullOrEmpty(strLeft)) {
                    List<string> arguments = StringManager.GetArguments(strLeft);
                    foreach(string argument in arguments) {
                        if(argument.StartsWith(VARIABLE_OPERATOR_VALUE, System.StringComparison.CurrentCultureIgnoreCase)) {
                            if(!goodLeft) goodLeft = true;
                            //newLeft = (string.IsNullOrEmpty(newLeft) ? "" : newLeft + ",") + GetQuoteString(Mid$(argument, 3));
                            newLeft = (string.IsNullOrEmpty(newLeft) ? "" : newLeft + ",") + argument;
                        } else if(argument.StartsWith("&", System.StringComparison.CurrentCultureIgnoreCase)) {
                            if(!goodLeft) goodLeft = true;
                            //newLeft = (string.IsNullOrEmpty(newLeft) ? "" : newLeft + ",") + GetQuoteString(Mid$(argument, 2));
                            newLeft = (string.IsNullOrEmpty(newLeft) ? "" : newLeft + ",") + argument;
                        } else {
                            newLeft = (string.IsNullOrEmpty(newLeft) ? "" : newLeft + ",") + argument;
                        }
                    } // dwArg
                }

                string newRight = "";
                if(!string.IsNullOrEmpty(strRight)) {
                    List<string> arguments = StringManager.GetArguments(strRight);
                    foreach(string argument in arguments) {
                        //if(argument.StartsWith(VARIABLE_OPERATOR_VALUE, System.StringComparison.CurrentCultureIgnoreCase)) {
                        //    newRight = (string.IsNullOrEmpty(newRight) ? "" : newRight + ",") + GetQuoteString(Mid$(argument, 3));
                        //} else if(argument.StartsWith("&", System.StringComparison.CurrentCultureIgnoreCase)) {
                        //    newRight = (string.IsNullOrEmpty(newRight) ? "" : newRight + ",") + GetQuoteString(Mid$(argument, 2));
                        //} else {
                        newRight = (string.IsNullOrEmpty(newRight) ? "" : newRight + ",") + argument;
                        //}
                    } // dwArg
                }

                outVariableNames  = newLeft;
                outVariableValues = newRight;
                if(goodLeft && !string.IsNullOrEmpty(newRight)) return true;
            }

            return false;
        } //IsVarSetLine

        // Get(yyy[index], [def])
        // Rules:
        //  * A single function line.
        private static bool IsVarGetLine(string line, out string outVariableName, out string outVariableDefault)
        {
            outVariableName = "";
            outVariableDefault = "";

            if(line.StartsWith(SyntaxVariableGetStart, System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(FunctionArgumentsEnd, System.StringComparison.CurrentCultureIgnoreCase)) {
                string variables = line.Substring(SyntaxVariableGetStart.Length);
                variables = StringManager.GetQuoteString(line.Substring(0, variables.Length - 1));

                List<string> variablesList; StringManager.SmartSplit(variables, ",", out variablesList);

                if(variablesList.Count == 2) {
                    outVariableName    = variablesList[0];
                    outVariableDefault = variablesList[1];
                    return true;
                }
            }

            return false;
        } //IsVarGetLine

        // Delete(yyy)
        // Rules:
        //  * A single function line.
        private static bool IsVarDeleteLine(string line, out string outVariableName)
        {
            outVariableName = "";

            if(line.StartsWith(SYNTAX_DELETE_START, System.StringComparison.CurrentCultureIgnoreCase) && line.EndsWith(FunctionArgumentsEnd, System.StringComparison.CurrentCultureIgnoreCase)) {
                outVariableName = line.Substring(SYNTAX_DELETE_START.Length);
                outVariableName = StringManager.GetQuoteString(line.Substring(0, outVariableName.Length - 1));
                return true;
            } else
                return false;
        } //IsVarDeleteLine

        // xxx.xxx(yyy, zzz, ...)
        // Rules:
        //  * Has Parenthesis: '(' and ')'.
        //  * The string before the parenthesis is the caller name, which must not have any spaces in it.
        //  * The caller name must have atleast one period.
        private static bool IsExternalSubLine(string line, out string outModuleName, out string outProcedureName, out string outArguments)
        {
            outModuleName    = "";
            outProcedureName = "";
            outArguments     = "";

            int startPos = StringManager.FindStringOutside(line, 1, "(");

            if(startPos > 0) {
                int endPos = StringManager.FindStringOutside(line, startPos + 1, ")");

                if(endPos > 0) {
                    string caller = line.Substring(0, startPos - 1);
                    if(string.IsNullOrEmpty(caller) || caller.StartsWith("&", System.StringComparison.CurrentCultureIgnoreCase)) return false;
                    
                    if(!caller.Contains(" ")) {
                        int pointPos = caller.IndexOf(".");

                        if(pointPos >= 0) {
                            outModuleName = StringManager.GetQuoteString(caller.Substring(0, pointPos - 1));
                            outProcedureName = StringManager.GetQuoteString(caller.Substring(pointPos));
                            //outArguments = FixArguments(line.Substring(startPos, (endPos - startPos) - 1));
                            outArguments = line.Substring(startPos, (endPos - startPos) - 1);
                            return true;
                        } else {
                            outModuleName = "";
                            outProcedureName = StringManager.GetQuoteString(caller);
                            //outArguments = FixArguments(line.Substring(startPos, (endPos - startPos) - 1));
                            outArguments = line.Substring(startPos, (endPos - startPos) - 1);
                            return true;
                        }
                    }
                }
            }

            return false;
        } //IsExternalSubLine
#endregion

#region "Parser"
        /// <returns>Return the new script, else return nothing.</returns>
        public static Framework.Script ParseFromFile(string sourceFileName, out string outScriptID)
        {
            // Get the data from the file.
            string scriptData = FileManager.GetFileContentsString(sourceFileName);
            // Parse the script from the file's data.
            Framework.Script outScript = ParseFromData(scriptData, out outScriptID);
            // if(unable to parse then return failed.
            if(outScript == null) return null;

            // Store the new file name.
            outScript.FileName = sourceFileName + ".dcs";
            // return the results.
            return outScript;
        } //ParseFromFile

        /// <returns>Return the new script, else return nothing.</returns>
        public static Framework.Script ParseFromData(string data, out string outScriptID)
        {
            outScriptID = "";
            // if(there is no data then ... return failure.
            if(data == null || data.Length == 0) return null;

            // Prepare the new script.
            Framework.Script newScript = new Framework.Script();

            // Parse the data into seperate lines.
            List<string> linesList = null;
            StringManager.SplitDataToLines(data, out linesList);
            if(linesList == null || linesList.Count == 0) return null;

            // Check the compiler setting lines, if failed then ... exit this procedure.
            int compilerVersion; string strScriptID = "";
            if(!CheckCompilerSettingsFromLines(linesList, out compilerVersion, out strScriptID)) return null;

            // if(a script ID was returned then ...
            if(!string.IsNullOrEmpty(strScriptID)) {
                // ... Add the event.
                Framework.Event newEvent = newScript.Events.Add(Framework.EventCommands.ScriptID, false);
                newEvent.Parameters.AddValue(strScriptID);
            }

            // ... Start the loop to read the file ...
            for(int dataLine = 0; dataLine < linesList.Count; dataLine++) {
                // Set the last line that we checked (to help storing errors.)
                Globals.Errors.LastLine = (1 + dataLine);

                // ... Fix the line ...
                string theLine = StringManager.FixLine(linesList[dataLine]);

                // if(the line isn't a compiler command line then ... check and parse the line.
                if(!theLine.StartsWith("@")) ParseLine(theLine, newScript);
            } //dataLine

            // return successful.
            outScriptID = strScriptID;
            return newScript;
        } //ParseFromData

        public static bool CheckCompilerSettingsFromFile(string fileName, out int outCompilerVersion, out string outScriptID)
        {
            string strData = FileManager.GetFileContentsString(fileName);
            return CheckCompilerSettingsFromData(strData, out outCompilerVersion, out outScriptID);
        } //CheckCompilerSettingsFromFile

        public static bool CheckCompilerSettingsFromData(string data, out int outCompilerVersion, out string outScriptID)
        {
            outCompilerVersion = 0;
            outScriptID = "";
            if(string.IsNullOrEmpty(data)) return false;

            List<string> linesList = null;
            StringManager.SplitDataToLines(data, out linesList);

            // return results.
            return CheckCompilerSettingsFromLines(linesList, out outCompilerVersion, out outScriptID);
        } //CheckCompilerSettingsFromData

        public static bool CheckCompilerSettingsFromLines(List<string> lines, out int outCompilerVersion, out string outScriptID)
        {
            outCompilerVersion = 0;
            outScriptID = "";
            if(lines == null || lines.Count == 0) return false;

            // ... Check for compiler setting lines ...
            bool foundHeader = false, foundVersion = false;
            for(int lineNumber = 1; lineNumber <= lines.Count; lineNumber++) {
                Globals.Errors.LastLine = lineNumber;

                string lineData = StringManager.FixLine(lines[lineNumber - 1]);

                // if(the line is a compiler command line then ...
                if(lineData.StartsWith("@")) {
                    // ... if(this line is the header line then ...
                    if(lineData.Equals("@" + RapidCodeHeader, System.StringComparison.CurrentCultureIgnoreCase)) {
                        // ... Notify that the header was found.
                        foundHeader = true;
                        // ... if(this line is the version line then ...
                    } else if(lineData.StartsWith("@Parser Version:", System.StringComparison.CurrentCultureIgnoreCase)) {
                        string strTemp = Right(lineData, lineData.Length - "@Parser Version:".Length).Trim();
                        if(System.Convert.ToInt32(strTemp) >= RapidCodeVersionMinimum && System.Convert.ToInt32(strTemp) <= RapidCodeVersionCurrent) {
                            outCompilerVersion = System.Convert.ToInt32(strTemp);
                            foundVersion = true;
                        }
                        // ... if(this line is the ID line then ...
                    } else if(lineData.StartsWith("@ID:", System.StringComparison.CurrentCultureIgnoreCase) || lineData.StartsWith("@ID=", System.StringComparison.CurrentCultureIgnoreCase)) {
                        string strTemp = Right(lineData, lineData.Length - "@ID:".Length).Trim();
                        outScriptID = strTemp;
                    }
                }
            } //lineNumber

            // if(the header wasn't found then ... exit this procedure.
            if(!foundHeader) return false;
            if(!foundVersion) return false;

            // return successful.
            return true;
        } //CheckCompilerSettingsFromLines

        /// <summary>Checks a parameter's string value to see if there is any internal code in it.</summary>
        /// Rules:
        ///   * Check for if the line is an external sub line.
        private static string CheckParameterValue(string str, Framework.EventsCollection pointers, out bool outAsPointer)
        {
            // Set default results.
            string results = StringManager.FixSpecialStrings(str);
            outAsPointer = false;

            // if(the parameter is in quotes then (don't touch anything inside) ...
            if(StringManager.IsQuoteString(results))
                return StringManager.GetQuoteString(results);
            else {
                // if(this contains a external sub caller, then ...
                string strExternalProc = "", strExternalModule = "", externalArguments = "";
                if(IsFunctionCallLine(results, out strExternalProc, out externalArguments)) {
                    string pointerName = "POINTER" + (pointers.Count + 1) + System.Math.Round(System.Convert.ToDouble(System.Environment.TickCount), 0).ToString();
                    // ... Split up the arguments ...
                    List<string> argumentsList; StringManager.SmartSplit(externalArguments, ",", out argumentsList);
                    // ... Add the event ...
                    Framework.Event newEvent = pointers.Add(pointerName, Framework.EventCommands.Function, false);
                    newEvent.Parameters.AddValue(Parser.FunctionTypes.Call.ToString());
                    newEvent.Parameters.AddValue(strExternalProc);
                    newEvent.Parameters.AddValue("0");
                    if(argumentsList.Count > 0) {
                        for(int argumentIndex = 0; argumentIndex < argumentsList.Count; argumentIndex++) {
                            bool asPointer = false;
                            argumentsList[argumentIndex] = CheckParameterValue(argumentsList[argumentIndex], pointers, out asPointer);
                            if(asPointer) {
                                newEvent.Parameters.AddFunction(argumentsList[argumentIndex]);
                            } else {
                                newEvent.Parameters.AddValue(argumentsList[argumentIndex]);
                            }
                        } //argumentIndex
                    }
                    // ... Notify that this added a pointer ...
                    outAsPointer = true;
                    // ... return the results.
                    return pointerName;
                } else if(IsExternalSubLine(results, out strExternalModule, out strExternalProc, out externalArguments)) {
                    string pointerName = "POINTER" + (pointers.Count + 1) + System.Math.Round(System.Convert.ToDouble(System.Environment.TickCount), 0).ToString();
                    // ... Split up the arguments ...
                    List<string> argumentsList; StringManager.SmartSplit(externalArguments, ",", out argumentsList);
                    // ... Add the event ...
                    Framework.Event newEvent = pointers.Add(pointerName, Framework.EventCommands.External, false);
                    newEvent.Parameters.AddValue(strExternalModule);
                    newEvent.Parameters.AddValue(strExternalProc);
                    if(argumentsList.Count > 0) {
                        for(int argumentIndex = 0; argumentIndex < argumentsList.Count; argumentIndex++) {
                            bool asPointer = false;
                            argumentsList[argumentIndex] = CheckParameterValue(argumentsList[argumentIndex], pointers, out asPointer);
                            if(asPointer) {
                                newEvent.Parameters.AddFunction(argumentsList[argumentIndex]);
                            } else {
                                newEvent.Parameters.AddValue(argumentsList[argumentIndex]);
                            }
                        } //argumentIndex
                    }
                    // ... Notify that this added a pointer ...
                    outAsPointer = true;
                    // ... return the results.
                    return pointerName;
                } else {
                    return results;
                }
            }
        } //CheckParameterValue

        public static void ParseLine(string line, Framework.Script script)
        {
            ParseLine(line, script.Events, script.Pointers);
        } //ParseLine

        public static void ParseLine(string line, Framework.EventsCollection events, Framework.EventsCollection pointers)
        {
            if(string.IsNullOrEmpty(line)) return;

            bool bIsDisabled = false;
            if(line.StartsWith("'")) {
                bIsDisabled = true;
                line = line.Substring(1);
            }

            // Check for comments, if a comment was found then ...
            string strComment = "";
            Framework.Event newEvent;
            if(StringManager.HasCommentOnLine(line, out line, out strComment)) {
                newEvent = events.Add(Framework.EventCommands.Comment, bIsDisabled);
                newEvent.Parameters.AddValue(StringManager.GetQuoteString(strComment));
                if(string.IsNullOrEmpty(line)) return;
            }

            // Declare some variables for the test functions.
            string strFunctionName = "", strFunctionArgs = "";
            Parser.FunctionAccess eFunctionAccess;
            string strReturnValue = "";
            string strBookmarkName = "";
            Parser.Comparisons eIfOperator;
            string strIfLeft = "", strIfRight = "";
            string strExpression = "";
            string strVarName = "", strVarValue = "";
            Parser.VariableScopes eVarScope;
            string strExternalProc = "", strExternalModule = "", strExternalArgs = "";
            // if(this is an empty line then ...
            if(string.IsNullOrEmpty(line)) {
                // ... Add the event.
                newEvent = events.Add(Framework.EventCommands.Nothing, bIsDisabled);
            } else if(IsFunctionStartLine(line, out strFunctionName, out strFunctionArgs, out eFunctionAccess)) {
                // ... Add the event.
                newEvent = events.Add(Framework.EventCommands.Function, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.FunctionTypes.Header.ToString());
                newEvent.Parameters.AddValue(strFunctionName);
                newEvent.Parameters.AddValue(eFunctionAccess.ToString());
                if(!string.IsNullOrEmpty(strFunctionArgs)) {
                    List<string> argumentsList; StringManager.SmartSplit(strFunctionArgs, ",", out argumentsList);
                    for(int argumentIndex = 0; argumentIndex < argumentsList.Count; argumentIndex++) {
                        newEvent.Parameters.AddValue(argumentsList[argumentIndex].Trim());
                    } //argumentIndex
                }
            } else if(IsFunctionEndLine(line)) {
                // ... Add the event.
                newEvent = events.Add(Framework.EventCommands.Function, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.FunctionTypes.End.ToString());
            } else if(IsFunctionReturnLine(line, out strReturnValue)) {
                // ... Add the event.
                newEvent = events.Add(Framework.EventCommands.Function, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.FunctionTypes.Results.ToString());

                if(!string.IsNullOrEmpty(strReturnValue)) {
                    List<string> argumentsList = StringManager.GetArguments(strReturnValue);
                    for(int argumentIndex = 0; argumentIndex < argumentsList.Count; argumentIndex++) {
                        argumentsList[argumentIndex] = StringManager.GetQuoteString(argumentsList[argumentIndex]);
                        bool asPointer = false;
                        argumentsList[argumentIndex] = CheckParameterValue(argumentsList[argumentIndex], pointers, out asPointer);
                        if(!asPointer) {
                            newEvent.Parameters.AddValue(argumentsList[argumentIndex]);
                        } else {
                            newEvent.Parameters.AddFunction(argumentsList[argumentIndex]);
                        }
                    } //argumentIndex
                }
            } else if(IsFunctionCallLine(line, out strFunctionName, out strFunctionArgs)) {
                bool asPointer = false;
                strFunctionName = CheckParameterValue(strFunctionName, pointers, out asPointer);
                //
                // ... Add the event.
                newEvent = events.Add(Framework.EventCommands.Function, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.FunctionTypes.Call.ToString());
                if(!asPointer) {
                    newEvent.Parameters.AddValue(strFunctionName);
                } else {
                    newEvent.Parameters.AddFunction(strFunctionName);
                }
                newEvent.Parameters.AddValue("0");
                //
                if(strFunctionArgs.Length != 0) {
                    List<string> argumentsList = StringManager.GetArguments(strFunctionArgs);
                    for(int argumentIndex = 0; argumentIndex < argumentsList.Count; argumentIndex++) {
                        argumentsList[argumentIndex] = CheckParameterValue(argumentsList[argumentIndex], pointers, out asPointer);
                        if(!asPointer) {
                            newEvent.Parameters.AddValue(argumentsList[argumentIndex]);
                        } else {
                            newEvent.Parameters.AddFunction(argumentsList[argumentIndex]);
                        }
                    } //argumentIndex
                }
            } else if(IsDoStatement(line, out strExpression)) {
                bool asPointer = false;
                strExpression = CheckParameterValue(strExpression, pointers, out asPointer);
                // ... Add the event.
                newEvent = events.Add(Framework.EventCommands.DoExpression, bIsDisabled);
                if(!asPointer) {
                    newEvent.Parameters.AddValue(strExpression);
                } else {
                    newEvent.Parameters.AddFunction(strExpression);
                }

            } else if(IsVarSetLine(line, out strVarName, out strVarValue)) {
                newEvent = events.Add(Framework.EventCommands.VariableCommand, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.VariableTypes.Set.ToString());
                //Framework.EventParameters_AddValue(Events.Item(dwEvent-1).Parameters, strVarName);
                //strVarValue = DSSL_Parse_RapidCode_CheckParameterValue(strVarValue, Pointers, asPointer(0));
                //Framework.EventParameters_Add(Events.Item(dwEvent-1).Parameters, strVarValue, asPointer(0));

                // Add the variable names count
                List<string> argumentList = null;
                if(!string.IsNullOrEmpty(strVarName)) argumentList = StringManager.GetArguments(strVarName);
                newEvent.Parameters.AddValue(argumentList.Count.ToString());

                // First add the variable names
                if(strVarName.Length != 0) {
                    for(int dwArg = 0; dwArg < argumentList.Count; dwArg++) {
                        newEvent.Parameters.AddValue(argumentList[dwArg]);
                    } // dwArg
                }

                // Add the variable values count
                argumentList = null;
                if(strVarName.Length != 0) argumentList = StringManager.GetArguments(strVarValue);
                newEvent.Parameters.AddValue(argumentList.Count.ToString());

                // } // add the variable values
                if(strVarValue.Length != 0) {
                    for(int argumentIndex = 0; argumentIndex < argumentList.Count; argumentIndex++) {
                        bool asPointer = false;
                        argumentList[argumentIndex] = CheckParameterValue(argumentList[argumentIndex], pointers, out asPointer);
                        if(!asPointer) {
                            newEvent.Parameters.AddValue(argumentList[argumentIndex]);
                        } else {
                            newEvent.Parameters.AddFunction(argumentList[argumentIndex]);
                        }
                    } //argumentIndex
                }
            } else if(IsVarDeleteLine(line, out strVarName)) {
                newEvent = events.Add(Framework.EventCommands.VariableCommand, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.VariableTypes.Remove.ToString());
                //Framework.EventParameters_AddValue(Events.Item(dwEvent-1).Parameters, strVarName)

                // Add the variable names count
                List<string> argumentList = new List<string>();
                if(strVarName.Length > 0) argumentList = StringManager.GetArguments(strVarName);
                newEvent.Parameters.AddValue(argumentList.Count.ToString());

                // First add the variable names
                if(strVarName.Length != 0) {
                    for(int argumentIndex = 0; argumentIndex < argumentList.Count; argumentIndex++) {
                        newEvent.Parameters.AddValue(argumentList[argumentIndex]);
                    } //argumentIndex
                }
            } else if(IsVarGetLine(line, out strVarName, out strVarValue)) {
                newEvent = events.Add(Framework.EventCommands.VariableCommand, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.VariableTypes.Get.ToString());
                newEvent.Parameters.AddValue(strVarName);
                newEvent.Parameters.AddValue(strVarValue);
            } else if(IsVarAddLine(line, out  strVarName, out strVarValue, out eVarScope)) {
                newEvent = events.Add(Framework.EventCommands.VariableCommand, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.VariableTypes.Add.ToString());
                newEvent.Parameters.AddValue(strVarName);
                newEvent.Parameters.AddValue(strVarValue);
                newEvent.Parameters.AddValue(eVarScope.ToString());

            } else if(IsLoopStartLine(line)) {
                newEvent = events.Add(Framework.EventCommands.LoopStatement, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.LoopTypes.Start.ToString());
            } else if(IsLoopEndLine(line)) {
                newEvent = events.Add(Framework.EventCommands.LoopStatement, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.LoopTypes.End.ToString());
            } else if(IsLoopBreakLine(line)) {
                newEvent = events.Add(Framework.EventCommands.LoopStatement, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.LoopTypes.Stop.ToString());
            } else if(IsIfStartLine(line, out eIfOperator, out strIfLeft, out strIfRight)) {
                bool[] asPointer = new bool[2];
                strIfLeft = CheckParameterValue(strIfLeft, pointers, out asPointer[0]);
                strIfRight = CheckParameterValue(strIfRight, pointers, out asPointer[1]);
                //
                newEvent = events.Add(Framework.EventCommands.IfStatement, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.IfStatementTypes.If.ToString());
                newEvent.Parameters.AddValue(eIfOperator.ToString());
                if(!asPointer[0]) {
                    newEvent.Parameters.AddValue(strIfLeft);
                } else {
                    newEvent.Parameters.AddFunction(strIfLeft);
                }
                if(!asPointer[1]) {
                    newEvent.Parameters.AddValue(strIfRight);
                } else {
                    newEvent.Parameters.AddFunction(strIfRight);
                }
            } else if(IsIfElseIfLine(line, out eIfOperator, out strIfLeft, out strIfRight)) {
                bool[] asPointer = new bool[2];
                strIfLeft = CheckParameterValue(strIfLeft, pointers, out asPointer[0]);
                strIfRight = CheckParameterValue(strIfRight, pointers, out asPointer[1]);
                //
                newEvent = events.Add(Framework.EventCommands.IfStatement, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.IfStatementTypes.ElseIf.ToString());
                newEvent.Parameters.AddValue(eIfOperator.ToString());
                if(!asPointer[0]) {
                    newEvent.Parameters.AddValue(strIfLeft);
                } else {
                    newEvent.Parameters.AddFunction(strIfLeft);
                }
                if(!asPointer[1]) {
                    newEvent.Parameters.AddValue(strIfRight);
                } else {
                    newEvent.Parameters.AddFunction(strIfRight);
                }
            } else if(IsIfElseLine(line)) {
                newEvent = events.Add(Framework.EventCommands.IfStatement, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.IfStatementTypes.Else.ToString());
            } else if(IsIfEndLine(line)) {
                newEvent = events.Add(Framework.EventCommands.IfStatement, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.IfStatementTypes.EndIf.ToString());
            } else if(IsBookMarkSetLine(line, out strBookmarkName)) {
                bool asPointer = false;
                strBookmarkName = CheckParameterValue(strBookmarkName, pointers, out asPointer);
                //
                newEvent = events.Add(Framework.EventCommands.BookmarkCommand, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.BookmarkTypes.Set.ToString());
                if(!asPointer) {
                    newEvent.Parameters.AddValue(strBookmarkName);
                } else {
                    newEvent.Parameters.AddFunction(strBookmarkName);
                }
            } else if(IsBookMarkGotoLine(line, out strBookmarkName)) {
                bool asPointer = false;
                strBookmarkName = CheckParameterValue(strBookmarkName, pointers, out asPointer);
                //
                newEvent = events.Add(Framework.EventCommands.BookmarkCommand, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.BookmarkTypes.GoTo.ToString());
                if(!asPointer) {
                    newEvent.Parameters.AddValue(strBookmarkName);
                } else {
                    newEvent.Parameters.AddFunction(strBookmarkName);
                }

            } else if(IsIncludeLine(line, out strExpression)) {
                bool asPointer = false;
                strExpression = CheckParameterValue(strExpression, pointers, out asPointer);
                //
                newEvent = events.Add(Framework.EventCommands.Include, bIsDisabled);
                if(!asPointer) {
                    newEvent.Parameters.AddValue(strExpression);
                } else {
                    newEvent.Parameters.AddFunction(strExpression);
                }

            } else if(IsStopLine(line)) {
                newEvent = events.Add(Framework.EventCommands.Stop, bIsDisabled);

            } else if(IsExternalSubLine(line, out strExternalModule, out strExternalProc, out strExternalArgs)) {
                List<string> argumentsList = StringManager.GetArguments(strExternalArgs);
                // ... Add the event.
                newEvent = events.Add(Framework.EventCommands.External, bIsDisabled);
                newEvent.Parameters.AddValue(strExternalModule);
                newEvent.Parameters.AddValue(strExternalProc);
                if(argumentsList != null && argumentsList.Count > 0) {
                    for(int argumentIndex = 0; argumentIndex < argumentsList.Count; argumentIndex++) {
                        bool asPointer = false;
                        argumentsList[argumentIndex] = CheckParameterValue(argumentsList[argumentIndex], pointers, out asPointer);
                        if(!asPointer) {
                            newEvent.Parameters.AddValue(argumentsList[argumentIndex]);
                        } else {
                            newEvent.Parameters.AddFunction(argumentsList[argumentIndex]);
                        }
                    } //argumentIndex
                }
            } else if(IsDecisionStartLine(line, out strExpression)) {
                bool asPointer = false;
                strExpression = CheckParameterValue(strExpression, pointers, out asPointer);
                //
                newEvent = events.Add(Framework.EventCommands.DecisionCondition, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.DecisionTypes.Start.ToString());
                if(!asPointer) {
                    newEvent.Parameters.AddValue(strExpression);
                } else {
                    newEvent.Parameters.AddFunction(strExpression);
                }
            } else if(IsDecisionItemLine(line, out strExpression, out eIfOperator)) {
                if(string.Equals(strExpression, "else", System.StringComparison.CurrentCultureIgnoreCase)) {
                    newEvent = events.Add(Framework.EventCommands.DecisionCondition, bIsDisabled);
                    newEvent.Parameters.AddValue(Parser.DecisionTypes.Default.ToString());
                } else {
                    bool asPointer = false;
                    strExpression = CheckParameterValue(strExpression, pointers, out asPointer);
                    //
                    newEvent = events.Add(Framework.EventCommands.DecisionCondition, bIsDisabled);
                    newEvent.Parameters.AddValue(Parser.DecisionTypes.Item.ToString());
                    if(!asPointer) {
                        newEvent.Parameters.AddValue(strExpression);
                    } else {
                        newEvent.Parameters.AddFunction(strExpression);
                    }
                    newEvent.Parameters.AddValue(eIfOperator.ToString());
                }
            } else if(IsDecisionEndLine(line)) {
                newEvent = events.Add(Framework.EventCommands.DecisionCondition, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.DecisionTypes.End.ToString());
            } else if(IsDecisionDefaultLine(line)) {
                newEvent = events.Add(Framework.EventCommands.DecisionCondition, bIsDisabled);
                newEvent.Parameters.AddValue(Parser.DecisionTypes.Default.ToString());

                //} else if(DSSL_Parse_RapidCode_IsGetSizeLine(Line, strExpression)) {
                //    newEvent = Framework.Events_Add(Events, DSSLEVENTCMD_GETSIZE, bIsDisabled)
                //    //
                //    strExpression = DSSL_Parse_RapidCode_CheckParameterValue(strExpression, Pointers, asPointer(0))
                //    Framework.EventParameters_Add(Events.item(dwEvent-1).Parameters, strExpression, asPointer(0))

            } else {
                Globals.Errors.Add("Unknown Line: " + line, Globals.Errors.LastLine);
                if(Settings.TreatUnknownLinesAsComments) {
                    // ... Add the event.
                    newEvent = events.Add(Framework.EventCommands.Comment, bIsDisabled);
                    newEvent.Parameters.AddValue(line);
                }
            }
        } //ParseLine
#endregion //Parser
    } //RapidCodeParser
} //DSSL namespace
