using System.Runtime.InteropServices;
using System.Text;

namespace YamlOracle.Worker;

/// <summary>
/// The part of libfyaml's API (include/libfyaml/libfyaml-core.h, 1.0.0-rc2) the worker uses. Everything goes through
/// exported functions; the only struct read directly is fy_event's first field, its type. The CI job that runs the
/// yaml-test-suite through the worker checks these declarations: a wrong one shows up there as failed cases.
/// </summary>
static unsafe partial class Native
{
    const string Lib = "fyaml";

    // enum fy_event_type
    public const int StreamStart = 1, StreamEnd = 2, DocumentStart = 3, DocumentEnd = 4, MappingStart = 5, MappingEnd = 6,
        SequenceStart = 7, SequenceEnd = 8, Scalar = 9, Alias = 10;

    // enum fy_scalar_style
    public const int Plain = 0, SingleQuoted = 1, DoubleQuoted = 2, Literal = 3, Folded = 4;

    // enum fy_node_style
    public const int Flow = 0;

    // enum fy_parse_cfg_flags: FYPCF_QUIET
    public const uint Quiet = 1u << 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct DiagError // struct fy_diag_error
    {
        public int Type;
        public int Module;
        public nint Token;
        public byte* Message;
        public byte* File;
        public int Line;
        public int Column;
    }

    [LibraryImport(Lib, EntryPoint = "fy_parser_get_diag")] public static partial nint ParserGetDiag(nint parser);
    [LibraryImport(Lib, EntryPoint = "fy_diag_unref")] public static partial void DiagUnref(nint diag);
    [LibraryImport(Lib, EntryPoint = "fy_diag_set_collect_errors")] public static partial void DiagSetCollectErrors(nint diag, [MarshalAs(UnmanagedType.U1)] bool collect);

    /// <summary>Keeps the parser's errors for <see cref="FirstError"/> instead of writing them to stderr.</summary>
    public static void CollectErrors(nint parser)
    {
        var diag = ParserGetDiag(parser);
        if (diag == 0) throw new InvalidOperationException("fy_parser_get_diag failed");
        DiagSetCollectErrors(diag, true);
        DiagUnref(diag);
    }
    [LibraryImport(Lib, EntryPoint = "fy_diag_errors_iterate")] public static partial DiagError* DiagErrorsIterate(nint diag, nint* previous);

    /// <summary>The first error the parser collected, as "line:column: message".</summary>
    public static string? FirstError(nint parser)
    {
        var diag = ParserGetDiag(parser);
        if (diag == 0) return null;
        try
        {
            nint previous = 0;
            var error = DiagErrorsIterate(diag, &previous);
            if (error is null) return null;
            var message = error->Message is null ? "" : Marshal.PtrToStringUTF8((nint)error->Message);
            return $"{error->Line}:{error->Column}: {message}";
        }
        finally { DiagUnref(diag); }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ParseCfg // struct fy_parse_cfg
    {
        public nint SearchPath;
        public uint Flags;
        public nint UserData;
        public nint Diag;
    }

    [LibraryImport(Lib, EntryPoint = "fy_parser_create")] public static partial nint ParserCreate(ParseCfg* cfg);
    [LibraryImport(Lib, EntryPoint = "fy_parser_destroy")] public static partial void ParserDestroy(nint parser);
    [LibraryImport(Lib, EntryPoint = "fy_parser_set_string")] public static partial int ParserSetString(nint parser, byte* str, nuint length);
    [LibraryImport(Lib, EntryPoint = "fy_parser_parse")] public static partial nint ParserParse(nint parser);
    [LibraryImport(Lib, EntryPoint = "fy_parser_event_free")] public static partial void ParserEventFree(nint parser, nint ev);

    [LibraryImport(Lib, EntryPoint = "fy_parser_get_stream_error")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool ParserGetStreamError(nint parser);

    [LibraryImport(Lib, EntryPoint = "fy_document_event_is_implicit")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool DocumentEventIsImplicit(nint ev);

    [LibraryImport(Lib, EntryPoint = "fy_event_get_token")] public static partial nint EventGetToken(nint ev);
    [LibraryImport(Lib, EntryPoint = "fy_event_get_anchor_token")] public static partial nint EventGetAnchorToken(nint ev);
    [LibraryImport(Lib, EntryPoint = "fy_event_get_tag_token")] public static partial nint EventGetTagToken(nint ev);
    [LibraryImport(Lib, EntryPoint = "fy_event_get_node_style")] public static partial int EventGetNodeStyle(nint ev);
    [LibraryImport(Lib, EntryPoint = "fy_token_scalar_style")] public static partial int TokenScalarStyle(nint token);
    [LibraryImport(Lib, EntryPoint = "fy_token_get_text")] public static partial byte* TokenGetText(nint token, nuint* length);

    /// <summary>fy_event_get_type is an inline function: the type is the struct's first field, an enum.</summary>
    public static int EventType(nint ev) => *(int*)ev;

    public static string? Text(nint token)
    {
        if (token == 0) return null;
        nuint length;
        var text = TokenGetText(token, &length);
        return text is null ? null : Encoding.UTF8.GetString(text, checked((int)length));
    }
}
