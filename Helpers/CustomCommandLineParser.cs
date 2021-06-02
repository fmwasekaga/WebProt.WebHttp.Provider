using CommandLineParser;
using CommandLineParser.Arguments;
using CommandLineParser.Exceptions;
using CommandLineParser.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebProt.WebHttp.Provider.Helpers
{
    /// <summary>
    /// CustomCommandLineParser allows user to define command line arguments and then parse
    /// the arguments from the command line.
    /// </summary>
    public class CustomCommandLineParser : CommandLineParser.CommandLineParser
    {
        public CustomCommandLineParser()
        {

        }

        public CustomCommandLineParser(string[] args)
        {
            //_argsNotParsed = args;
            ParseCommandLine(args, true);
        }


        #region property backing fields

        private List<Argument> _arguments = new List<Argument>();

        private List<ArgumentCertification> _certifications = new List<ArgumentCertification>();

        private Dictionary<char, Argument> _shortNameLookup;

        private Dictionary<string, Argument> _longNameLookup;

        readonly Dictionary<string, Argument> _ignoreCaseLookupDirectory = new Dictionary<string, Argument>();

        private string[] _argsNotParsed;

        private bool _checkMandatoryArguments = true;

        private bool _checkArgumentCertifications = true;

        private bool _allowShortSwitchGrouping = true;

        private readonly AdditionalArgumentsSettings _additionalArgumentsSettings = new AdditionalArgumentsSettings();

        private readonly List<string> _showUsageCommands = new List<string> { "--help", "/?", "/help" };

        private bool _acceptSlash = true;

        private bool _acceptHyphen = true;

        private bool _ignoreCase;

        private char[] equalsSignSyntaxValuesSeparators = new char[] { ',', ';' };

        private static Regex lettersOnly = new Regex("^[a-zA-Z]$");

        #endregion

        /// <summary>
        /// Defined command line arguments
        /// </summary>
        public new List<Argument> Arguments
        {
            get { return _arguments; }
            set { _arguments = value; }
        }

        /// <summary>
        /// Set of <see cref="ArgumentCertification">certifications</see> - certifications can be used to define 
        /// which argument combinations are allowed and such type of validations. 
        /// </summary>
        /// <seealso cref="CheckArgumentCertifications"/>
        /// <seealso cref="ArgumentCertification"/>
        /// <seealso cref="ArgumentGroupCertification"/>
        /// <seealso cref="DistinctGroupsCertification"/>
        public new List<ArgumentCertification> Certifications
        {
            get { return _certifications; }
            set { _certifications = value; }
        }

        /// <summary>
        /// Allows more specific definition of additional arguments 
        /// (arguments after those with - and -- prefix).
        /// </summary>
        public new AdditionalArgumentsSettings AdditionalArgumentsSettings
        {
            get { return _additionalArgumentsSettings; }
        }

        /// <summary>
        /// Text printed in the beginning of 'show usage'
        /// </summary>
        public new string ShowUsageHeader { get; set; }

        /// <summary>
        /// Text printed in the end of 'show usage'
        /// </summary>
        public new string ShowUsageFooter { get; set; }

        /// <summary>
        /// Arguments that directly invoke <see cref="ShowUsage()"/>. By default this is --help and /?.
        /// </summary>
        public new IList<string> ShowUsageCommands
        {
            get
            {
                return _showUsageCommands;
            }
        }

        /// <summary>
        /// When set to true, usage help is printed on the console when command line is without arguments.
        /// Default is false. 
        /// </summary>
        public new bool ShowUsageOnEmptyCommandline { get; set; }

        /// <summary>
        /// When set to true, <see cref="MandatoryArgumentNotSetException"/> is thrown when some of the non-optional argument
        /// is not found on the command line. Default is true.
        /// See: <see cref="Argument.Optional"/>
        /// </summary>
        public new bool CheckMandatoryArguments
        {
            get { return _checkMandatoryArguments; }
            set { _checkMandatoryArguments = value; }
        }

        /// <summary>
        /// When set to true, arguments are certified (using set of <see cref="Certifications"/>) after parsing. 
        /// Default is true.
        /// </summary>
        public new bool CheckArgumentCertifications
        {
            get { return _checkArgumentCertifications; }
            set { _checkArgumentCertifications = value; }
        }

        /// <summary>
        /// When set to true (default) <see cref="SwitchArgument">switch arguments</see> can be grouped on the command line. 
        /// (e.g. -a -b -c can be written as -abc). When set to false and such a group is found, <see cref="CommandLineFormatException"/> is thrown.
        /// </summary>
        public new bool AllowShortSwitchGrouping
        {
            get { return _allowShortSwitchGrouping; }
            set { _allowShortSwitchGrouping = value; }
        }

        /// <summary>
        /// Allows arguments in /a and /arg format
        /// </summary>
        public new bool AcceptSlash
        {
            get { return _acceptSlash; }
            set { _acceptSlash = value; }
        }

        /// <summary>
        /// Allows arguments in -a and --arg format
        /// </summary>
        public new bool AcceptHyphen
        {
            get { return _acceptHyphen; }
            set { _acceptHyphen = value; }
        }

        /// <summary>
        /// Argument names case insensitive (--OUTPUT or --output are treated equally)
        /// </summary>
        public new bool IgnoreCase
        {
            get { return _ignoreCase; }
            set { _ignoreCase = value; }
        }

        /// <summary>
        /// When set to true, values of <see cref="ValueArgument{TValue}"/> are separeted by space, 
        /// otherwise, they are separeted by equal sign and enclosed in quotation marks
        /// </summary>
        /// <example>
        /// --output="somefile.txt"
        /// </example>
        public new bool AcceptEqualSignSyntaxForValueArguments { get; set; }

        public new bool PreserveValueQuotesForEqualsSignSyntax { get; set; }

        public new char[] EqualsSignSyntaxValuesSeparators
        {
            get
            {
                return equalsSignSyntaxValuesSeparators;
            }

            set
            {
                equalsSignSyntaxValuesSeparators = value;
            }
        }

        /// <summary>
        /// Value is set to true after parsing finishes successfuly 
        /// </summary>
        public new bool ParsingSucceeded { get; private set; }

        /// <summary>
        /// Fills lookup dictionaries with arguments names and aliases 
        /// </summary>
        private void InitializeArgumentLookupDictionaries()
        {
            _shortNameLookup = new Dictionary<char, Argument>();
            _longNameLookup = new Dictionary<string, Argument>();
            foreach (Argument argument in _arguments)
            {
                if (argument.ShortName.HasValue)
                {
                    _shortNameLookup.Add(argument.ShortName.Value, argument);
                }
                foreach (char aliasChar in argument.ShortAliases)
                {
                    _shortNameLookup.Add(aliasChar, argument);
                }
                if (!string.IsNullOrEmpty(argument.LongName))
                {
                    _longNameLookup.Add(argument.LongName, argument);
                }
                foreach (string aliasString in argument.LongAliases)
                {
                    _longNameLookup.Add(aliasString, argument);
                }
            }

            _ignoreCaseLookupDirectory.Clear();
            if (IgnoreCase)
            {
                var allLookups = _shortNameLookup
                    .Select(kvp => new KeyValuePair<string, Argument>(kvp.Key.ToString(), kvp.Value))
                    .Concat(_longNameLookup);
                foreach (KeyValuePair<string, Argument> keyValuePair in allLookups)
                {
                    var icString = keyValuePair.Key.ToString().ToUpper();
                    if (_ignoreCaseLookupDirectory.ContainsKey(icString))
                    {
                        throw new ArgumentException("Clash in ignore case argument names: " + icString);
                    }
                    _ignoreCaseLookupDirectory.Add(icString, keyValuePair.Value);
                }
            }
        }

        /// <summary>
        /// Resolves arguments from the command line and calls <see cref="Argument.Parse"/> on each argument. 
		/// Additional arguments are stored in AdditionalArgumentsSettings.AdditionalArguments 
		/// if AdditionalArgumentsSettings.AcceptAdditionalArguments is set to true. 
        /// </summary>
        /// <exception cref="CommandLineFormatException">Command line arguments are not in correct format</exception>
        /// <param name="args">Command line arguments</param>
        public void ParseCommandLine(string[] args, bool skipErrors = false)
        {
            ParsingSucceeded = false;
            _arguments.ForEach(action => action.Init());
            List<string> argsList = new List<string>(args);
            InitializeArgumentLookupDictionaries();
            ExpandValueArgumentsWithEqualSigns(argsList);
            ExpandShortSwitches(argsList);
            AdditionalArgumentsSettings.AdditionalArguments = new string[0];

            _argsNotParsed = args;

            if ((args.Length == 0 && ShowUsageOnEmptyCommandline) ||
                (args.Length == 1 && _showUsageCommands.Contains(args[0])))
            {
                ShowUsage();
                return;
            }

            if (args.Length > 0)
            {
                int argIndex;

                for (argIndex = 0; argIndex < argsList.Count;)
                {
                    string curArg = argsList[argIndex];
                    Argument argument = ParseArgument(curArg, skipErrors);
                    if (argument == null) argIndex += 2; //break;
                    else
                    {
                        argument.Parse(argsList, ref argIndex);
                        argument.UpdateBoundObject();
                    }
                }

                ParseAdditionalArguments(argsList, argIndex);
            }

            foreach (Argument argument in _arguments)
            {
                if (argument is IArgumentWithDefaultValue && !argument.Parsed)
                {
                    argument.UpdateBoundObject();
                }
            }

            PerformMandatoryArgumentsCheck();
            PerformCertificationCheck();
            ParsingSucceeded = true;
        }

        public void ParseCommandLine()
        {
            ParseCommandLine(_argsNotParsed);
        }

        /// <summary>
        /// Parses one argument on the command line, lookups argument in <see cref="Arguments"/> using 
        /// lookup dictionaries.
        /// </summary>
        /// <param name="curArg">argument string (including '-' or '--' prefixes)</param>
        /// <returns>Look-uped Argument class</returns>
        /// <exception cref="CommandLineFormatException">Command line is in the wrong format</exception>
        /// <exception cref="UnknownArgumentException">Unknown argument found.</exception>
        private Argument ParseArgument(string curArg, bool skipErrors = false)
        {
            if (curArg[0] == '-')
            {
                if (AcceptHyphen)
                {
                    string argName;
                    if (curArg.Length > 1)
                    {
                        if (curArg[1] == '-')
                        {
                            //long name
                            argName = curArg.Substring(2);
                            if (argName.Length == 1)
                            {
                                if (!skipErrors) throw new CommandLineFormatException(String.Format(
                                    Messages.EXC_FORMAT_SHORTNAME_PREFIX, argName));
                                else return null;
                            }

                        }
                        else
                        {
                            //short name
                            argName = curArg.Substring(1);
                            if (argName.Length != 1)
                            {
                                if (!skipErrors) throw new CommandLineFormatException(
                                     String.Format(Messages.EXC_FORMAT_LONGNAME_PREFIX, argName));
                                else return null;
                            }
                        }

                        Argument argument = LookupArgument(argName);
                        if (argument != null) return argument;
                        //else if (!skipErrors)
                        //    throw new UnknownArgumentException(string.Format(Messages.EXC_ARG_UNKNOWN, argName), argName);
                        else return null;
                    }
                    else
                    {
                        if (!skipErrors) throw new CommandLineFormatException(Messages.EXC_FORMAT_SINGLEHYPHEN);
                        else return null;
                    }
                }
                else
                    return null;
            }
            else if (curArg[0] == '/')
            {
                if (AcceptSlash)
                {
                    if (curArg.Length > 1)
                    {
                        if (curArg[1] == '/')
                        {
                            if (!skipErrors) throw new CommandLineFormatException(Messages.EXC_FORMAT_SINGLESLASH);
                        }
                        string argName = curArg.Substring(1);
                        Argument argument = LookupArgument(argName);
                        if (argument != null) return argument;
                        //else if (!skipErrors) throw new UnknownArgumentException(string.Format(Messages.EXC_ARG_UNKNOWN, argName), argName);
                        else return null;
                    }
                    else
                    {
                        if (!skipErrors) throw new CommandLineFormatException(Messages.EXC_FORMAT_DOUBLESLASH);
                        else return null;
                    }
                }
                else
                    return null;
            }
            else
                /*
                 * curArg does not start with '-' character and therefore it is considered additional argument.
                 * Argument parsing ends here.
                 */
                return null;
        }

        /// <summary>
        /// Checks whether or non-optional arguments were defined on the command line. 
        /// </summary>
        /// <exception cref="MandatoryArgumentNotSetException"><see cref="Argument.Optional">Non-optional</see> argument not defined.</exception>
        /// <seealso cref="CheckMandatoryArguments"/>, <seealso cref="Argument.Optional"/>
        private void PerformMandatoryArgumentsCheck()
        {
            _arguments.ForEach(delegate (Argument arg)
            {
                if (!arg.Optional && !arg.Parsed)
                {
                    var name = string.Empty;
                    if (arg.ShortName.HasValue && !string.IsNullOrEmpty(arg.LongName))
                    {
                        name = string.Format("{0}({1})", arg.ShortName, arg.LongName);
                    }
                    if (!string.IsNullOrEmpty(arg.LongName))
                    {
                        name = arg.LongName;
                    }
                    if (arg.ShortName.HasValue)
                    {
                        name = arg.ShortName.ToString();
                    }
                    
                    throw new MandatoryArgumentNotSetException(string.Format(Messages.EXC_MISSING_MANDATORY_ARGUMENT, name), name);
                }
                    
            });
        }

        /// <summary>
        /// Performs certifications
        /// </summary>
        private void PerformCertificationCheck()
        {
            _certifications.ForEach(delegate (ArgumentCertification certification)
            {
                certification.Certify(this);
            });
        }

        /// <summary>
        /// Parses the rest of the command line for additional arguments
        /// </summary>
        /// <param name="argsList">list of thearguments</param>
        /// <param name="i">index of the first additional argument in <paramref name="argsList"/></param>
        /// <exception cref="CommandLineFormatException">Additional arguments found, but they are 
        /// not accepted</exception>
        private void ParseAdditionalArguments(List<string> argsList, int i)
        {
            if (AdditionalArgumentsSettings.AcceptAdditionalArguments)
            {
                AdditionalArgumentsSettings.AdditionalArguments = new string[argsList.Count - i];
                if (i < argsList.Count)
                {
                    Array.Copy(argsList.ToArray(), i, AdditionalArgumentsSettings.AdditionalArguments, 0, argsList.Count - i);
                }
                AdditionalArgumentsSettings.ProcessArguments();
            }
            else if (i < argsList.Count)
            {
                // only throw when there are any additional arguments
                throw new CommandLineFormatException(
                    Messages.EXC_ADDITIONAL_ARGUMENTS_FOUND);
            }
        }

        /// <summary>
        /// If <see cref="AllowShortSwitchGrouping"/> is set to true,  each group of switch arguments (e. g. -abcd) 
        /// is expanded into full format (-a -b -c -d) in the list.
        /// </summary>
        /// <exception cref="CommandLineFormatException">Argument of type differnt from SwitchArgument found in one of the groups. </exception>
        /// <param name="argsList">List of arguments</param>
        /// <exception cref="CommandLineFormatException">Arguments that are not <see cref="SwitchArgument">switches</see> found 
        /// in a group.</exception>
        /// <seealso cref="AllowShortSwitchGrouping"/>
        private void ExpandShortSwitches(IList<string> argsList)
        {
            if (AllowShortSwitchGrouping)
            {
                for (int i = 0; i < argsList.Count; i++)
                {
                    string arg = argsList[i];
                    if (arg.Length > 2)
                    {
                        if (arg[0] == '/' && arg[1] != '/' && AcceptSlash && _longNameLookup.ContainsKey(arg.Substring(1)))
                            continue;
                        if (arg.Contains('='))
                            continue;
                        if (ShowUsageCommands.Contains(arg))
                            continue;

                        char sep = arg[0];
                        if ((arg[0] == '-' && AcceptHyphen && lettersOnly.IsMatch(arg.Substring(1)))
                            || (arg[0] == '/' && AcceptSlash && lettersOnly.IsMatch(arg.Substring(1))))
                        {
                            argsList.RemoveAt(i);
                            //arg ~ -xyz
                            foreach (char c in arg.Substring(1))
                            {
                                if (_shortNameLookup.ContainsKey(c) && !(_shortNameLookup[c] is SwitchArgument))
                                {
                                    throw new CommandLineFormatException(
                                        string.Format(Messages.EXC_BAD_ARG_IN_GROUP, c));
                                }

                                argsList.Insert(i, sep.ToString() + c);
                                i++;
                            }
                        }
                    }
                }
            }
        }

        private void ExpandValueArgumentsWithEqualSigns(IList<string> argsList)
        {
            if (AcceptEqualSignSyntaxForValueArguments)
            {
                for (int i = 0; i < argsList.Count; i++)
                {
                    string arg = argsList[i];

                    Regex r = new Regex("([^=]*)=(.*)");
                    if (AcceptEqualSignSyntaxForValueArguments && r.IsMatch(arg))
                    {
                        Match m = r.Match(arg);
                        string argNameWithSep = m.Groups[1].Value;
                        string argName = argNameWithSep;
                        while (argName.StartsWith("-") && AcceptHyphen)
                            argName = argName.Substring(1);
                        while (argName.StartsWith("/") && AcceptSlash)
                            argName = argName.Substring(1);
                        string argValue = m.Groups[2].Value;
                        if (!PreserveValueQuotesForEqualsSignSyntax && !string.IsNullOrEmpty(argValue) && argValue.StartsWith("\"") && argValue.EndsWith("\""))
                        {
                            argValue = argValue.Trim('"');
                        }

                        Argument argument = LookupArgument(argName);
                        if (argument is IValueArgument)
                        {
                            argsList.RemoveAt(i);
                            if (argument.AllowMultiple)
                            {
                                var splitted = argValue.Split(equalsSignSyntaxValuesSeparators);
                                foreach (var singleValue in splitted)
                                {
                                    argsList.Insert(i, argNameWithSep);
                                    i++;
                                    if (!string.IsNullOrEmpty(singleValue))
                                    {
                                        argsList.Insert(i, singleValue);
                                        i++;
                                    }
                                }
                                i--;
                            }
                            else
                            {
                                argsList.Insert(i, argNameWithSep);
                                i++;
                                argsList.Insert(i, argValue);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns argument of given name
        /// </summary>
        /// <param name="argName">Name of the argument (<see cref="Argument.ShortName"/>, <see cref="Argument.LongName"/>, or alias)</param>
        /// <returns>Found argument or null when argument is not present</returns>
        public new Argument LookupArgument(string argName)
        {
            if (argName.Length == 1)
            {
                if (_shortNameLookup.ContainsKey(argName[0]))
                {
                    return _shortNameLookup[argName[0]];
                }
            }
            else
            {
                if (_longNameLookup.ContainsKey(argName))
                {
                    return _longNameLookup[argName];
                }
            }
            if (IgnoreCase && _ignoreCaseLookupDirectory.ContainsKey(argName.ToUpper()))
            {
                return _ignoreCaseLookupDirectory[argName.ToUpper()];
            }
            // argument not found anywhere
            return null;
        }
    }

    internal static class Messages
    {
        public static string CERT_REMARKS = "Argument combinations remarks:";

        public static string EXC_ADDITIONAL_ARGS_TOO_EARLY = "AdditionalArguments cannot be accessed before ParseCommandLine is called.";

        public static string EXC_ADDITONAL_ARGS_FORBIDDEN = "AcceptAdditionalArguments is set to false therefore AdditionalArguments can not be read.";

        public static string EXC_ARG_BOUNDED_GREATER_THAN_MAX = "Argument value { 0} is greater then maximum value {1}";

        public static string EXC_ARG_BOUNDED_LESSER_THAN_MIN = "Argument value {0} is lesser then minimum value {1}";

        public static string EXC_ARG_ENUM_OUT_OF_RANGE = "Value {0} is not allowed for argument {1}";

        public static string EXC_ARG_NOT_ONE_CHAR = "ShortName of an argument must not be whitespace character.";

        public static string EXC_ARG_NOT_ONE_WORD = "LongName of an argument must be one word.";

        public static string EXC_ARG_SWITCH_PRINT = "Argument: {0} value: {1}";

        public static string EXC_ARG_UNKNOWN = "Unknown argument found: {0}.";

        public static string EXC_ARG_VALUE_MISSING = "Value argument {0} must be followed by a value, another argument({1}) found instead";

        public static string EXC_ARG_VALUE_MISSING2 = "Value argument {0} must be followed by a value.";

        public static string EXC_ARG_VALUE_MULTIPLE_OCCURS = "Argument {0} can not be used multiple times.";

        public static string EXC_ARG_VALUE_PRINT = "Argument: {0}, type: {3}, value: {2} (converted from: {1})";

        public static string EXC_ARG_VALUE_PRINT_MULTIPLE = "Argument: {0}, type: {3}, occured {1}x values: {2}";

        public static string EXC_ARG_VALUE_STANDARD_CONVERT_FAILED = "Failed to convert string {0} to type {1}. Use strings in accepted format or define custom conversion using ConvertValueHandler.";

        public static string EXC_ARG_VALUE_STRINGVALUE_ACCESS = "Arguments StringValue can be read after ParseCommandLine is called.";

        public static string EXC_ARG_VALUE_USER_CONVERT_MISSING = "Type {0} of argument {1} is not a built-in type. Set ConvertValueHandler to a conversion routine for this type or define static method Parse(string stringValue, CultureInfo cultureInfo) that can Parse your type from string. ";

        public static string EXC_BAD_ARG_IN_GROUP = "Grouping of multiple short name arguments in one word(e.g. -a -b into -ab) is allowed only for switch arguments.Argument {0} is not a switch argument.";

        public static string EXC_BINDING = "Binding of the argument {0} to the field {1} of the object {2} failed.";

        public static string EXC_DIR_NOT_FOUND = "Directory not found : {0} and DirectoryMustExist flag is set to true.";

        public static string EXC_FILE_MUST_EXIST = "OpenFile should not be called when FileMustExist flag is not set.";

        public static string EXC_FILE_NOT_FOUND = "File not found : {0} and FileMustExist flag is set to true.";

        public static string EXC_FORMAT_LONGNAME_PREFIX = "Only short argument names(single character) are allowed after single '-' character(e.g. -v). For long names use double '-' format(e.g. '--ver'). Wrong argument is: {0}";

        public static string EXC_FORMAT_SHORTNAME_PREFIX = "If short name argument is used, it must be prefixed with single '-' character.Wrong argument is: {0}";

        public static string EXC_FORMAT_SINGLEHYPHEN = "Found character '-' not followed by an argument.";

        public static string EXC_GROUP_AT_LEAST_ONE = "At least one of these arguments: {0} must be used.";

        public static string EXC_GROUP_DISTINCT = "None of these arguments: {0} can be used together with any of these: {1}.";

        public static string EXC_GROUP_EXACTLY_ONE_MORE_USED = "Only one of these arguments: {0} can be used.";

        public static string EXC_GROUP_EXACTLY_ONE_NONE_USED = "One of these arguments: {0} must be used.";

        public static string EXC_GROUP_ONE_OR_NONE_MORE_USED = "These arguments can not be used together: {0}.";

        public static string EXC_GROUP_ARGUMENTS_REQUIRED_BY_ANOTHER_ARGUMENT = "Argument: {0} requires the following arguments: {1}.";

        public static string EXC_NONNEGATIVE = "The value must be non negative.";

        public static string MSG_ADDITIONAL_ARGUMENTS = "Additional arguments:";

        public static string MSG_COMMAND_LINE = "Command line:";

        public static string MSG_OPTIONAL = "[optional]";

        public static string MSG_NOT_PARSED_ARGUMENTS = "Arguments not specified:";

        public static string MSG_PARSED_ARGUMENTS = "Parsed Arguments:";

        public static string MSG_PARSING_RESULTS = "Parsing results:";

        public static string MSG_USAGE = "Usage:";

        public static string EXC_MISSING_MANDATORY_ARGUMENT = "Argument {0} is not marked as optional and was not found on the command line.";

        public static string EXC_ADDITIONAL_ARGUMENTS_FOUND = "Additional arguments found and parser does not accept additional arguments. Set AcceptAdditionalArguments to true if you want to accept them. ";

        public static string EXC_NOT_ENOUGH_ADDITIONAL_ARGUMENTS = "Not enough additional arguments. Needed {0} additional arguments.";

        public static string MSG_EXAMPLE_FORMAT = "Example: {0}";

        public static string EXC_GROUP_ALL_OR_NONE_USED_NOT_ALL_USED = "All or none of these arguments: {0} must be used.";

        public static string EXC_GROUP_ALL_USED_NOT_ALL_USED = "All of these arguments: {0} must be used.";

        public static string GROUP_ALL_OR_NONE_USED = "All or none of these arguments: {0} must be used.";

        public static string GROUP_ALL_USED = "All of these arguments: {0} must be used.";

        public static string GROUP_AT_LEAST_ONE_USED = "At least one of these arguments: {0} must be used.";

        public static string GROUP_EXACTLY_ONE_USED = "One (and only one) of these arguments: {0} must be used.";

        public static string GROUP_ONE_OR_NONE_USED = "These arguments can not be used together: {0}.";

        public static string EXC_FORMAT_DOUBLESLASH = "Invalid sequence \"//\" in the command line.";

        public static string EXC_FORMAT_SINGLESLASH = "Found character '/' not followed by an argument.";
    }
}
