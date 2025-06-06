// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace rec FSharp.Compiler.Syntax

open System
open System.Diagnostics
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.Xml
open FSharp.Compiler.SyntaxTrivia

[<Struct; NoEquality; NoComparison; DebuggerDisplay("{idText}")>]
type Ident(text: string, range: range) =
    member _.idText = text
    member _.idRange = range
    override _.ToString() = text

type SynIdent =
    | SynIdent of ident: Ident * trivia: IdentTrivia option

    member this.Range =
        match this with
        | SynIdent(ident, trivia) ->
            match trivia with
            | Some value ->
                match value with
                | IdentTrivia.OriginalNotationWithParen(leftParen, _, rightParen)
                | IdentTrivia.HasParenthesis(leftParen, rightParen) -> unionRanges leftParen rightParen
                | _ -> ident.idRange
            | None -> ident.idRange

type LongIdent = Ident list

type SynLongIdent =
    | SynLongIdent of id: LongIdent * dotRanges: range list * trivia: IdentTrivia option list

    member this.Range =
        match this with
        | SynLongIdent([], _, _) -> failwith "rangeOfLid"
        | SynLongIdent([ id ], [], _) -> id.idRange
        | SynLongIdent([ id ], [ m ], _) -> unionRanges id.idRange m
        | SynLongIdent(h :: t, [], _) -> unionRanges h.idRange (List.last t).idRange
        | SynLongIdent(h :: t, dotRanges, _) -> unionRanges h.idRange (List.last t).idRange |> unionRanges (List.last dotRanges)

    member this.LongIdent =
        match this with
        | SynLongIdent(lid, _, _) -> lid

    member this.Dots =
        match this with
        | SynLongIdent(dotRanges = dots) -> dots

    member this.Trivia =
        match this with
        | SynLongIdent(trivia = trivia) -> List.choose id trivia

    member this.IdentsWithTrivia =
        let (SynLongIdent(lid, _, trivia)) = this

        if lid.Length = trivia.Length then
            List.zip lid trivia |> List.map SynIdent
        elif lid.Length > trivia.Length then
            let delta = lid.Length - trivia.Length
            let trivia = [ yield! trivia; yield! List.replicate delta None ]
            List.zip lid trivia |> List.map SynIdent
        else
            failwith "difference between idents and trivia"

    member this.ThereIsAnExtraDotAtTheEnd =
        match this with
        | SynLongIdent(lid, dots, _) -> lid.Length = dots.Length

    member this.RangeWithoutAnyExtraDot =
        match this with
        | SynLongIdent([], _, _) -> failwith "rangeOfLidwd"
        | SynLongIdent([ id ], _, _) -> id.idRange
        | SynLongIdent(h :: t, dotRanges, _) ->
            let nonExtraDots =
                if dotRanges.Length = t.Length then
                    dotRanges
                else
                    List.truncate t.Length dotRanges

            unionRanges h.idRange (List.last t).idRange
            |> unionRanges (List.last nonExtraDots)

[<AutoOpen>]
module SynLongIdentHelpers =
    [<Obsolete("Please use SynLongIdent or define a custom active pattern")>]
    let (|LongIdentWithDots|) =
        function
        | SynLongIdent(lid, dots, _) -> lid, dots

    [<Obsolete("Please use SynLongIdent")>]
    let LongIdentWithDots (lid, dots) =
        SynLongIdent(lid, dots, List.replicate lid.Length None)

[<RequireQualifiedAccess>]
type ParserDetail =
    | Ok

    | ErrorRecovery

[<RequireQualifiedAccess>]
type TyparStaticReq =
    | None

    | HeadType

[<NoEquality; NoComparison>]
type SynTypar =
    | SynTypar of ident: Ident * staticReq: TyparStaticReq * isCompGen: bool

    member this.Range =
        match this with
        | SynTypar(id, _, _) -> id.idRange

[<Struct; RequireQualifiedAccess>]
type SynStringKind =
    | Regular
    | Verbatim
    | TripleQuote

[<Struct; RequireQualifiedAccess>]
type SynByteStringKind =
    | Regular
    | Verbatim

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynConst =

    | Unit

    | Bool of bool

    | SByte of sbyte

    | Byte of byte

    | Int16 of int16

    | UInt16 of uint16

    | Int32 of int32

    | UInt32 of uint32

    | Int64 of int64

    | UInt64 of uint64

    | IntPtr of int64

    | UIntPtr of uint64

    | Single of single

    | Double of double

    | Char of char

    | Decimal of System.Decimal

    | UserNum of value: string * suffix: string

    | String of text: string * synStringKind: SynStringKind * range: range

    | Bytes of bytes: byte[] * synByteStringKind: SynByteStringKind * range: range

    | UInt16s of uint16[]

    | Measure of constant: SynConst * constantRange: range * synMeasure: SynMeasure * trivia: SynMeasureConstantTrivia

    | SourceIdentifier of constant: string * value: string * range: range

    member c.Range dflt =
        match c with
        | SynConst.String(_, _, m0)
        | SynConst.Bytes(_, _, m0)
        | SynConst.SourceIdentifier(_, _, m0) -> m0
        | _ -> dflt

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynMeasure =

    | Named of longId: LongIdent * range: range

    | Product of measure1: SynMeasure * mAsterisk: range * measure2: SynMeasure * range: range

    | Seq of measures: SynMeasure list * range: range

    | Divide of measure1: SynMeasure option * mSlash: range * measure2: SynMeasure * range: range

    | Power of measure: SynMeasure * caretRange: range * power: SynRationalConst * range: range

    | One of range: range

    | Anon of range: range

    | Var of typar: SynTypar * range: range

    | Paren of measure: SynMeasure * range: range

    member x.Range =
        match x with
        | SynMeasure.Named(range = m)
        | SynMeasure.Product(range = m)
        | SynMeasure.Seq(range = m)
        | SynMeasure.Divide(range = m)
        | SynMeasure.Power(range = m)
        | SynMeasure.One(range = m)
        | SynMeasure.Anon(range = m)
        | SynMeasure.Var(range = m)
        | SynMeasure.Paren(range = m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynRationalConst =

    | Integer of value: int32 * range: range

    | Rational of numerator: int32 * numeratorRange: range * divRange: range * denominator: int32 * denominatorRange: range * range: range

    | Negate of rationalConst: SynRationalConst * range: range

    | Paren of rationalConst: SynRationalConst * range: range

[<RequireQualifiedAccess>]
type SynAccess =
    | Public of range: range

    | Internal of range: range

    | Private of range: range

    override this.ToString() =
        match this with
        | Public _ -> "Public"
        | Internal _ -> "Internal"
        | Private _ -> "Private"

    member this.Range: range =
        match this with
        | Public m
        | Internal m
        | Private m -> m

[<RequireQualifiedAccess>]
type DebugPointAtTarget =
    | Yes
    | No

[<RequireQualifiedAccess>]
type DebugPointAtSequential =
    | SuppressNeither
    | SuppressStmt
    | SuppressBoth
    | SuppressExpr

[<RequireQualifiedAccess>]
type DebugPointAtTry =
    | Yes of range: range
    | No

[<RequireQualifiedAccess>]
type DebugPointAtLeafExpr = Yes of range

[<RequireQualifiedAccess>]
type DebugPointAtWith =
    | Yes of range: range
    | No

[<RequireQualifiedAccess>]
type DebugPointAtFinally =
    | Yes of range: range
    | No

[<RequireQualifiedAccess>]
type DebugPointAtFor =
    | Yes of range: range
    | No

[<RequireQualifiedAccess>]
type DebugPointAtInOrTo =
    | Yes of range: range
    | No

[<RequireQualifiedAccess>]
type DebugPointAtWhile =
    | Yes of range: range
    | No

[<RequireQualifiedAccess>]
type DebugPointAtBinding =
    | Yes of range: range
    | NoneAtDo
    | NoneAtLet
    | NoneAtSticky
    | NoneAtInvisible

    member x.Combine(y: DebugPointAtBinding) =
        match x, y with
        | DebugPointAtBinding.Yes _ as g, _ -> g
        | _, (DebugPointAtBinding.Yes _ as g) -> g
        | _ -> x

type SeqExprOnly = SeqExprOnly of bool

type BlockSeparator = range * pos option

type RecordFieldName = SynLongIdent * bool

type ExprAtomicFlag =
    | Atomic = 0
    | NonAtomic = 1

[<RequireQualifiedAccess>]
type SynBindingKind =

    // Indicates 'expr' in a module, via ElimSynModuleDeclExpr
    | StandaloneExpression

    | Normal

    // Covers 'do expr' in class or let-rec but not in a module.
    //
    // Note, in a module 'expr' corresponds to SynModuleDecl.Expr
    // Note, in a module 'do expr' corresponds to SynModuleDecl.Expr with expression SynExpr.Do
    // These are eliminated to bindings with 'StandaloneExpression' by ElimSynModuleDeclExpr
    | Do

[<NoEquality; NoComparison>]
type SynTyparDecl =
    | SynTyparDecl of attributes: SynAttributes * typar: SynTypar * intersectionConstraints: SynType list * trivia: SynTyparDeclTrivia

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeConstraint =

    | WhereTyparIsValueType of typar: SynTypar * range: range

    | WhereTyparIsReferenceType of typar: SynTypar * range: range

    | WhereTyparIsUnmanaged of typar: SynTypar * range: range

    | WhereTyparSupportsNull of typar: SynTypar * range: range

    | WhereTyparNotSupportsNull of genericName: SynTypar * range: range * trivia: SynTypeConstraintWhereTyparNotSupportsNullTrivia

    | WhereTyparIsComparable of typar: SynTypar * range: range

    | WhereTyparIsEquatable of typar: SynTypar * range: range

    | WhereTyparDefaultsToType of typar: SynTypar * typeName: SynType * range: range

    | WhereTyparSubtypeOfType of typar: SynTypar * typeName: SynType * range: range

    | WhereTyparSupportsMember of typars: SynType * memberSig: SynMemberSig * range: range

    | WhereTyparIsEnum of typar: SynTypar * typeArgs: SynType list * range: range

    | WhereTyparIsDelegate of typar: SynTypar * typeArgs: SynType list * range: range

    | WhereSelfConstrained of selfConstraint: SynType * range: range

    member x.Range =
        match x with
        | WhereTyparIsValueType(range = range)
        | WhereTyparIsReferenceType(range = range)
        | WhereTyparIsUnmanaged(range = range)
        | WhereTyparSupportsNull(range = range)
        | WhereTyparNotSupportsNull(range = range)
        | WhereTyparIsComparable(range = range)
        | WhereTyparIsEquatable(range = range)
        | WhereTyparDefaultsToType(range = range)
        | WhereTyparSubtypeOfType(range = range)
        | WhereTyparSupportsMember(range = range)
        | WhereTyparIsEnum(range = range)
        | WhereTyparIsDelegate(range = range) -> range
        | WhereSelfConstrained(range = range) -> range

[<RequireQualifiedAccess>]
type SynTyparDecls =
    | PostfixList of decls: SynTyparDecl list * constraints: SynTypeConstraint list * range: range
    | PrefixList of decls: SynTyparDecl list * range: range
    | SinglePrefix of decl: SynTyparDecl * range: range

    member x.TyparDecls =
        match x with
        | PostfixList(decls = decls)
        | PrefixList(decls = decls) -> decls
        | SinglePrefix(decl, _) -> [ decl ]

    member x.Constraints =
        match x with
        | PostfixList(decls = decls; constraints = constraints) ->
            // Synthesize SynTypeConstraints implied with any intersection constraints in SynTyparDecl
            // The parser makes sure we're only dealing with hash constraints here
            let intersectionConstraints =
                decls
                |> List.collect (fun (SynTyparDecl(typar = tp; intersectionConstraints = tys)) ->
                    tys
                    |> List.map (fun ty ->
                        let ty =
                            match ty with
                            | SynType.HashConstraint(ty, _) -> ty
                            | _ -> ty

                        SynTypeConstraint.WhereTyparSubtypeOfType(tp, ty, ty.Range)))

            List.append intersectionConstraints constraints
        | _ -> []

    member x.Range =
        match x with
        | PostfixList(range = range)
        | PrefixList(range = range)
        | SinglePrefix(range = range) -> range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTupleTypeSegment =
    | Type of typeName: SynType
    | Star of range: range
    | Slash of range: range

    member this.Range =
        match this with
        | SynTupleTypeSegment.Type t -> t.Range
        | SynTupleTypeSegment.Star(range = range)
        | SynTupleTypeSegment.Slash(range = range) -> range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynType =

    | LongIdent of longDotId: SynLongIdent

    | App of
        typeName: SynType *
        lessRange: range option *
        typeArgs: SynType list *
        commaRanges: range list *  // interstitial commas
        greaterRange: range option *
        isPostfix: bool *
        range: range

    | LongIdentApp of
        typeName: SynType *
        longDotId: SynLongIdent *
        lessRange: range option *
        typeArgs: SynType list *
        commaRanges: range list *  // interstitial commas
        greaterRange: range option *
        range: range

    | Tuple of isStruct: bool * path: SynTupleTypeSegment list * range: range

    | AnonRecd of isStruct: bool * fields: (Ident * SynType) list * range: range

    | Array of rank: int * elementType: SynType * range: range

    | Fun of argType: SynType * returnType: SynType * range: range * trivia: SynTypeFunTrivia

    | Var of typar: SynTypar * range: range

    | Anon of range: range

    | WithGlobalConstraints of typeName: SynType * constraints: SynTypeConstraint list * range: range

    | HashConstraint of innerType: SynType * range: range

    | MeasurePower of baseMeasure: SynType * exponent: SynRationalConst * range: range

    | StaticConstant of constant: SynConst * range: range

    | StaticConstantNull of range: range

    | StaticConstantExpr of expr: SynExpr * range: range

    | StaticConstantNamed of ident: SynType * value: SynType * range: range

    | WithNull of innerType: SynType * ambivalent: bool * range: range * trivia: SynTypeWithNullTrivia

    | Paren of innerType: SynType * range: range

    | SignatureParameter of attributes: SynAttributes * optional: bool * id: Ident option * usedType: SynType * range: range

    | Or of lhsType: SynType * rhsType: SynType * range: range * trivia: SynTypeOrTrivia

    | FromParseError of range: range

    | Intersection of typar: SynTypar option * types: SynType list * range: range * trivia: SynTyparDeclTrivia

    member x.Range =
        match x with
        | SynType.App(range = m)
        | SynType.LongIdentApp(range = m)
        | SynType.Tuple(range = m)
        | SynType.Array(range = m)
        | SynType.AnonRecd(range = m)
        | SynType.Fun(range = m)
        | SynType.Var(range = m)
        | SynType.Anon(range = m)
        | SynType.WithGlobalConstraints(range = m)
        | SynType.StaticConstant(range = m)
        | SynType.StaticConstantNull(range = m)
        | SynType.StaticConstantExpr(range = m)
        | SynType.StaticConstantNamed(range = m)
        | SynType.HashConstraint(range = m)
        | SynType.WithNull(range = m)
        | SynType.MeasurePower(range = m)
        | SynType.Paren(range = m)
        | SynType.SignatureParameter(range = m)
        | SynType.Or(range = m)
        | SynType.Intersection(range = m)
        | SynType.FromParseError(range = m) -> m
        | SynType.LongIdent lidwd -> lidwd.Range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynExpr =

    | Paren of expr: SynExpr * leftParenRange: range * rightParenRange: range option * range: range

    | Quote of operator: SynExpr * isRaw: bool * quotedExpr: SynExpr * isFromQueryExpression: bool * range: range

    | Const of constant: SynConst * range: range

    | Typed of expr: SynExpr * targetType: SynType * range: range

    | Tuple of
        isStruct: bool *
        exprs: SynExpr list *
        commaRanges: range list *  // interstitial commas
        range: range

    | AnonRecd of
        isStruct: bool *
        copyInfo: (SynExpr * BlockSeparator) option *
        recordFields: (SynLongIdent * range option * SynExpr) list *
        range: range *
        trivia: SynExprAnonRecdTrivia

    | ArrayOrList of isArray: bool * exprs: SynExpr list * range: range

    | Record of
        baseInfo: (SynType * SynExpr * range * BlockSeparator option * range) option *
        copyInfo: (SynExpr * BlockSeparator) option *
        recordFields: SynExprRecordField list *
        range: range

    | New of isProtected: bool * targetType: SynType * expr: SynExpr * range: range

    | ObjExpr of
        objType: SynType *
        argOptions: (SynExpr * Ident option) option *
        withKeyword: range option *
        bindings: SynBinding list *
        members: SynMemberDefns *
        extraImpls: SynInterfaceImpl list *
        newExprRange: range *
        range: range

    | While of whileDebugPoint: DebugPointAtWhile * whileExpr: SynExpr * doExpr: SynExpr * range: range

    | For of
        forDebugPoint: DebugPointAtFor *
        toDebugPoint: DebugPointAtInOrTo *
        ident: Ident *
        equalsRange: range option *
        identBody: SynExpr *
        direction: bool *
        toBody: SynExpr *
        doBody: SynExpr *
        range: range

    | ForEach of
        forDebugPoint: DebugPointAtFor *
        inDebugPoint: DebugPointAtInOrTo *
        seqExprOnly: SeqExprOnly *
        isFromSource: bool *
        pat: SynPat *
        enumExpr: SynExpr *
        bodyExpr: SynExpr *
        range: range

    | ArrayOrListComputed of isArray: bool * expr: SynExpr * range: range

    | IndexRange of expr1: SynExpr option * opm: range * expr2: SynExpr option * range1: range * range2: range * range: range

    | IndexFromEnd of expr: SynExpr * range: range

    | ComputationExpr of hasSeqBuilder: bool * expr: SynExpr * range: range

    | Lambda of
        fromMethod: bool *
        inLambdaSeq: bool *
        args: SynSimplePats *
        body: SynExpr *
        parsedData: (SynPat list * SynExpr) option *
        range: range *
        trivia: SynExprLambdaTrivia

    | MatchLambda of
        isExnMatch: bool *
        keywordRange: range *
        matchClauses: SynMatchClause list *
        matchDebugPoint: DebugPointAtBinding *
        range: range

    | Match of
        matchDebugPoint: DebugPointAtBinding *
        expr: SynExpr *
        clauses: SynMatchClause list *
        range: range *
        trivia: SynExprMatchTrivia

    | Do of expr: SynExpr * range: range

    | Assert of expr: SynExpr * range: range

    | App of flag: ExprAtomicFlag * isInfix: bool * funcExpr: SynExpr * argExpr: SynExpr * range: range

    | TypeApp of
        expr: SynExpr *
        lessRange: range *
        typeArgs: SynType list *
        commaRanges: range list *
        greaterRange: range option *
        typeArgsRange: range *
        range: range

    | LetOrUse of isRecursive: bool * isUse: bool * bindings: SynBinding list * body: SynExpr * range: range * trivia: SynExprLetOrUseTrivia

    | TryWith of
        tryExpr: SynExpr *
        withCases: SynMatchClause list *
        range: range *
        tryDebugPoint: DebugPointAtTry *
        withDebugPoint: DebugPointAtWith *
        trivia: SynExprTryWithTrivia

    | TryFinally of
        tryExpr: SynExpr *
        finallyExpr: SynExpr *
        range: range *
        tryDebugPoint: DebugPointAtTry *
        finallyDebugPoint: DebugPointAtFinally *
        trivia: SynExprTryFinallyTrivia

    | Lazy of expr: SynExpr * range: range

    | Sequential of
        debugPoint: DebugPointAtSequential *
        isTrueSeq: bool *
        expr1: SynExpr *
        expr2: SynExpr *
        range: range *
        trivia: SynExprSequentialTrivia

    | IfThenElse of
        ifExpr: SynExpr *
        thenExpr: SynExpr *
        elseExpr: SynExpr option *
        spIfToThen: DebugPointAtBinding *
        isFromErrorRecovery: bool *
        range: range *
        trivia: SynExprIfThenElseTrivia

    | Typar of typar: SynTypar * range: range

    | Ident of ident: Ident

    | LongIdent of isOptional: bool * longDotId: SynLongIdent * altNameRefCell: SynSimplePatAlternativeIdInfo ref option * range: range

    | LongIdentSet of longDotId: SynLongIdent * expr: SynExpr * range: range

    | DotGet of expr: SynExpr * rangeOfDot: range * longDotId: SynLongIdent * range: range

    | DotLambda of expr: SynExpr * range: range * trivia: SynExprDotLambdaTrivia

    | DotSet of targetExpr: SynExpr * longDotId: SynLongIdent * rhsExpr: SynExpr * range: range

    | Set of targetExpr: SynExpr * rhsExpr: SynExpr * range: range

    | DotIndexedGet of objectExpr: SynExpr * indexArgs: SynExpr * dotRange: range * range: range

    | DotIndexedSet of
        objectExpr: SynExpr *
        indexArgs: SynExpr *
        valueExpr: SynExpr *
        leftOfSetRange: range *
        dotRange: range *
        range: range

    | NamedIndexedPropertySet of longDotId: SynLongIdent * expr1: SynExpr * expr2: SynExpr * range: range

    | DotNamedIndexedPropertySet of targetExpr: SynExpr * longDotId: SynLongIdent * argExpr: SynExpr * rhsExpr: SynExpr * range: range

    | TypeTest of expr: SynExpr * targetType: SynType * range: range

    | Upcast of expr: SynExpr * targetType: SynType * range: range

    | Downcast of expr: SynExpr * targetType: SynType * range: range

    | InferredUpcast of expr: SynExpr * range: range

    | InferredDowncast of expr: SynExpr * range: range

    | Null of range: range

    | AddressOf of isByref: bool * expr: SynExpr * opRange: range * range: range

    | TraitCall of supportTys: SynType * traitSig: SynMemberSig * argExpr: SynExpr * range: range

    | JoinIn of lhsExpr: SynExpr * lhsRange: range * rhsExpr: SynExpr * range: range

    | ImplicitZero of range: range

    | SequentialOrImplicitYield of debugPoint: DebugPointAtSequential * expr1: SynExpr * expr2: SynExpr * ifNotStmt: SynExpr * range: range

    | YieldOrReturn of flags: (bool * bool) * expr: SynExpr * range: range * trivia: SynExprYieldOrReturnTrivia

    | YieldOrReturnFrom of flags: (bool * bool) * expr: SynExpr * range: range * trivia: SynExprYieldOrReturnFromTrivia

    | LetOrUseBang of
        bindDebugPoint: DebugPointAtBinding *
        isUse: bool *
        isFromSource: bool *
        pat: SynPat *
        rhs: SynExpr *
        andBangs: SynExprAndBang list *
        body: SynExpr *
        range: range *
        trivia: SynExprLetOrUseBangTrivia

    | MatchBang of
        matchDebugPoint: DebugPointAtBinding *
        expr: SynExpr *
        clauses: SynMatchClause list *
        range: range *
        trivia: SynExprMatchBangTrivia

    | DoBang of expr: SynExpr * range: range * trivia: SynExprDoBangTrivia

    | WhileBang of whileDebugPoint: DebugPointAtWhile * whileExpr: SynExpr * doExpr: SynExpr * range: range

    | LibraryOnlyILAssembly of
        ilCode: obj *  // this type is ILInstr[]  but is hidden to avoid the representation of AbstractIL being public
        typeArgs: SynType list *
        args: SynExpr list *
        retTy: SynType list *
        range: range

    | LibraryOnlyStaticOptimization of
        constraints: SynStaticOptimizationConstraint list *
        expr: SynExpr *
        optimizedExpr: SynExpr *
        range: range

    | LibraryOnlyUnionCaseFieldGet of expr: SynExpr * longId: LongIdent * fieldNum: int * range: range

    | LibraryOnlyUnionCaseFieldSet of expr: SynExpr * longId: LongIdent * fieldNum: int * rhsExpr: SynExpr * range: range

    | ArbitraryAfterError of debugStr: string * range: range

    | FromParseError of expr: SynExpr * range: range

    | DiscardAfterMissingQualificationAfterDot of expr: SynExpr * dotRange: range * range: range

    | Fixed of expr: SynExpr * range: range

    | InterpolatedString of contents: SynInterpolatedStringPart list * synStringKind: SynStringKind * range: range

    | DebugPoint of debugPoint: DebugPointAtLeafExpr * isControlFlow: bool * innerExpr: SynExpr

    | Dynamic of funcExpr: SynExpr * qmark: range * argExpr: SynExpr * range: range

    member e.Range =
        match e with
        | SynExpr.Paren(_, leftParenRange, rightParenRange, r) ->
            match rightParenRange with
            | Some rightParenRange when leftParenRange.FileIndex <> rightParenRange.FileIndex -> leftParenRange
            | _ -> r
        | SynExpr.Quote(range = m)
        | SynExpr.Const(range = m)
        | SynExpr.Typed(range = m)
        | SynExpr.Tuple(range = m)
        | SynExpr.AnonRecd(range = m)
        | SynExpr.ArrayOrList(range = m)
        | SynExpr.Record(range = m)
        | SynExpr.New(range = m)
        | SynExpr.ObjExpr(range = m)
        | SynExpr.While(range = m)
        | SynExpr.For(range = m)
        | SynExpr.ForEach(range = m)
        | SynExpr.ComputationExpr(range = m)
        | SynExpr.ArrayOrListComputed(range = m)
        | SynExpr.Lambda(range = m)
        | SynExpr.Match(range = m)
        | SynExpr.MatchLambda(range = m)
        | SynExpr.Do(range = m)
        | SynExpr.Assert(range = m)
        | SynExpr.App(range = m)
        | SynExpr.TypeApp(range = m)
        | SynExpr.LetOrUse(range = m)
        | SynExpr.TryWith(range = m)
        | SynExpr.TryFinally(range = m)
        | SynExpr.Sequential(range = m)
        | SynExpr.SequentialOrImplicitYield(range = m)
        | SynExpr.ArbitraryAfterError(range = m)
        | SynExpr.FromParseError(range = m)
        | SynExpr.DiscardAfterMissingQualificationAfterDot(range = m)
        | SynExpr.IfThenElse(range = m)
        | SynExpr.LongIdent(range = m)
        | SynExpr.LongIdentSet(range = m)
        | SynExpr.NamedIndexedPropertySet(range = m)
        | SynExpr.DotIndexedGet(range = m)
        | SynExpr.DotIndexedSet(range = m)
        | SynExpr.DotGet(range = m)
        | SynExpr.DotLambda(range = m)
        | SynExpr.DotSet(range = m)
        | SynExpr.Set(range = m)
        | SynExpr.DotNamedIndexedPropertySet(range = m)
        | SynExpr.LibraryOnlyUnionCaseFieldGet(range = m)
        | SynExpr.LibraryOnlyUnionCaseFieldSet(range = m)
        | SynExpr.LibraryOnlyILAssembly(range = m)
        | SynExpr.LibraryOnlyStaticOptimization(range = m)
        | SynExpr.IndexRange(range = m)
        | SynExpr.IndexFromEnd(range = m)
        | SynExpr.TypeTest(range = m)
        | SynExpr.Upcast(range = m)
        | SynExpr.AddressOf(range = m)
        | SynExpr.Downcast(range = m)
        | SynExpr.JoinIn(range = m)
        | SynExpr.InferredUpcast(range = m)
        | SynExpr.InferredDowncast(range = m)
        | SynExpr.Null(range = m)
        | SynExpr.Lazy(range = m)
        | SynExpr.TraitCall(range = m)
        | SynExpr.ImplicitZero(range = m)
        | SynExpr.YieldOrReturn(range = m)
        | SynExpr.YieldOrReturnFrom(range = m)
        | SynExpr.LetOrUseBang(range = m)
        | SynExpr.MatchBang(range = m)
        | SynExpr.DoBang(range = m)
        | SynExpr.WhileBang(range = m)
        | SynExpr.Fixed(range = m)
        | SynExpr.InterpolatedString(range = m)
        | SynExpr.Dynamic(range = m) -> m
        | SynExpr.Ident id -> id.idRange
        | SynExpr.Typar(range = m) -> m
        | SynExpr.DebugPoint(_, _, innerExpr) -> innerExpr.Range

    member e.RangeWithoutAnyExtraDot =
        match e with
        | SynExpr.DiscardAfterMissingQualificationAfterDot(expr, _, _) -> expr.Range
        | _ -> e.Range

    member e.RangeOfFirstPortion =
        match e with
        // these are better than just .Range, and also commonly applicable inside queries
        | SynExpr.Paren(_, m, _, _) -> m
        | SynExpr.Sequential(expr1 = e1)
        | SynExpr.SequentialOrImplicitYield(expr1 = e1)
        | SynExpr.App(funcExpr = e1) -> e1.RangeOfFirstPortion
        | SynExpr.ForEach(pat = pat; range = r) ->
            let e = (pat.Range: range).Start
            withEnd e r
        | _ -> e.Range

    member this.IsArbExprAndThusAlreadyReportedError =
        match this with
        | SynExpr.ArbitraryAfterError _ -> true
        | _ -> false

[<NoEquality; NoComparison>]
type SynExprAndBang =
    | SynExprAndBang of
        debugPoint: DebugPointAtBinding *
        isUse: bool *
        isFromSource: bool *
        pat: SynPat *
        body: SynExpr *
        range: range *
        trivia: SynExprAndBangTrivia

    member x.Range =
        match x with
        | SynExprAndBang(range = range) -> range

    member this.Trivia =
        match this with
        | SynExprAndBang(trivia = trivia) -> trivia

[<NoEquality; NoComparison>]
type SynExprRecordField =
    | SynExprRecordField of
        fieldName: RecordFieldName *
        equalsRange: range option *
        expr: SynExpr option *
        blockSeparator: BlockSeparator option

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynInterpolatedStringPart =
    | String of value: string * range: range
    | FillExpr of fillExpr: SynExpr * qualifiers: Ident option

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynSimplePat =
    | Id of
        ident: Ident *
        altNameRefCell: SynSimplePatAlternativeIdInfo ref option *
        isCompilerGenerated: bool *
        isThisVal: bool *
        isOptional: bool *
        range: range

    | Typed of pat: SynSimplePat * targetType: SynType * range: range

    | Attrib of pat: SynSimplePat * attributes: SynAttributes * range: range

    member x.Range =
        match x with
        | SynSimplePat.Id(range = range)
        | SynSimplePat.Typed(range = range)
        | SynSimplePat.Attrib(range = range) -> range

[<RequireQualifiedAccess>]
type SynSimplePatAlternativeIdInfo =

    | Undecided of Ident

    | Decided of Ident

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynStaticOptimizationConstraint =

    | WhenTyparTyconEqualsTycon of typar: SynTypar * rhsType: SynType * range: range

    | WhenTyparIsStruct of typar: SynTypar * range: range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynSimplePats =
    | SimplePats of pats: SynSimplePat list * commaRanges: range list * range: range

    member x.Range =
        match x with
        | SynSimplePats.SimplePats(range = range) -> range

[<RequireQualifiedAccess>]
type SynArgPats =
    | Pats of pats: SynPat list

    | NamePatPairs of pats: (Ident * range option * SynPat) list * range: range * trivia: SynArgPatsNamePatPairsTrivia

    member x.Patterns =
        match x with
        | Pats pats -> pats
        | NamePatPairs(pats = pats) -> pats |> List.map (fun (_, _, pat) -> pat)

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynPat =

    | Const of constant: SynConst * range: range

    | Wild of range: range

    | Named of ident: SynIdent * isThisVal: bool * accessibility: SynAccess option * range: range

    | Typed of pat: SynPat * targetType: SynType * range: range

    | Attrib of pat: SynPat * attributes: SynAttributes * range: range

    | Or of lhsPat: SynPat * rhsPat: SynPat * range: range * trivia: SynPatOrTrivia

    | ListCons of lhsPat: SynPat * rhsPat: SynPat * range: range * trivia: SynPatListConsTrivia

    | Ands of pats: SynPat list * range: range

    | As of lhsPat: SynPat * rhsPat: SynPat * range: range

    | LongIdent of
        longDotId: SynLongIdent *
        extraId: Ident option *  // holds additional ident for tooling
        typarDecls: SynValTyparDecls option *  // usually None: temporary used to parse "f<'a> x = x"
        argPats: SynArgPats *
        accessibility: SynAccess option *
        range: range

    | Tuple of isStruct: bool * elementPats: SynPat list * commaRanges: range list * range: range

    | Paren of pat: SynPat * range: range

    | ArrayOrList of isArray: bool * elementPats: SynPat list * range: range

    | Record of fieldPats: ((LongIdent * Ident) * range option * SynPat) list * range: range

    | Null of range: range

    | OptionalVal of ident: Ident * range: range

    | IsInst of pat: SynType * range: range

    | QuoteExpr of expr: SynExpr * range: range

    | InstanceMember of
        thisId: Ident *
        memberId: Ident *
        toolingId: Ident option *  // holds additional ident for tooling
        accessibility: SynAccess option *
        range: range

    | FromParseError of pat: SynPat * range: range

    member p.Range =
        match p with
        | SynPat.Const(range = m)
        | SynPat.Wild(range = m)
        | SynPat.Named(range = m)
        | SynPat.Or(range = m)
        | SynPat.ListCons(range = m)
        | SynPat.Ands(range = m)
        | SynPat.As(range = m)
        | SynPat.LongIdent(range = m)
        | SynPat.ArrayOrList(range = m)
        | SynPat.Tuple(range = m)
        | SynPat.Typed(range = m)
        | SynPat.Attrib(range = m)
        | SynPat.Record(range = m)
        | SynPat.Null(range = m)
        | SynPat.IsInst(range = m)
        | SynPat.QuoteExpr(range = m)
        | SynPat.InstanceMember(range = m)
        | SynPat.OptionalVal(range = m)
        | SynPat.Paren(range = m)
        | SynPat.FromParseError(range = m) -> m

[<NoEquality; NoComparison>]
type SynInterfaceImpl =
    | SynInterfaceImpl of
        interfaceTy: SynType *
        withKeyword: range option *
        bindings: SynBinding list *
        members: SynMemberDefns *
        range: range

[<NoEquality; NoComparison>]
type SynMatchClause =
    | SynMatchClause of
        pat: SynPat *
        whenExpr: SynExpr option *
        resultExpr: SynExpr *
        range: range *
        debugPoint: DebugPointAtTarget *
        trivia: SynMatchClauseTrivia

    member this.RangeOfGuardAndRhs =
        match this with
        | SynMatchClause(whenExpr = eo; resultExpr = e) ->
            match eo with
            | None -> e.Range
            | Some x -> unionRanges e.Range x.Range

    member this.IsTrueMatchClause =
        let (SynMatchClause(trivia = trivia)) = this
        trivia.BarRange.IsSome && trivia.ArrowRange.IsSome

    member this.Range =
        match this with
        | SynMatchClause(range = m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynAttribute =
    {
        TypeName: SynLongIdent

        ArgExpr: SynExpr

        Target: Ident option

        AppliesToGetterAndSetter: bool

        Range: range
    }

[<RequireQualifiedAccess>]
type SynAttributeList =
    {
        Attributes: SynAttribute list

        Range: range
    }

type SynAttributes = SynAttributeList list

[<NoEquality; NoComparison>]
type SynValData =
    | SynValData of memberFlags: SynMemberFlags option * valInfo: SynValInfo * thisIdOpt: Ident option

    member x.SynValInfo = (let (SynValData(valInfo = synValInfo)) = x in synValInfo)

[<NoEquality; NoComparison>]
type SynBinding =
    | SynBinding of
        accessibility: SynAccess option *
        kind: SynBindingKind *
        isInline: bool *
        isMutable: bool *
        attributes: SynAttributes *
        xmlDoc: PreXmlDoc *
        valData: SynValData *
        headPat: SynPat *
        returnInfo: SynBindingReturnInfo option *
        expr: SynExpr *
        range: range *
        debugPoint: DebugPointAtBinding *
        trivia: SynBindingTrivia

    // no member just named "Range", as that would be confusing:
    //  - for everything else, the 'range' member that appears last/second-to-last is the 'full range' of the whole tree construct
    //  - but for Binding, the 'range' is only the range of the left-hand-side, the right-hand-side range is in the SynExpr
    //  - so we use explicit names to avoid confusion
    member x.RangeOfBindingWithoutRhs = let (SynBinding(range = m)) = x in m

    member x.RangeOfBindingWithRhs =
        let (SynBinding(expr = e; range = m)) = x in unionRanges e.Range m

    member x.RangeOfHeadPattern = let (SynBinding(headPat = headPat)) = x in headPat.Range

[<NoEquality; NoComparison>]
type SynBindingReturnInfo =
    | SynBindingReturnInfo of typeName: SynType * range: range * attributes: SynAttributes * trivia: SynBindingReturnInfoTrivia

[<NoComparison; RequireQualifiedAccess; CustomEquality>]
type SynMemberFlags =
    {
        IsInstance: bool

        IsDispatchSlot: bool

        IsOverrideOrExplicitImpl: bool

        IsFinal: bool

        // This is not persisted in pickling
        GetterOrSetterIsCompilerGenerated: bool

        MemberKind: SynMemberKind
    }

    override this.Equals other =
        match other with
        | :? SynMemberFlags as other ->
            this.IsInstance = other.IsInstance
            && this.IsDispatchSlot = other.IsDispatchSlot
            && this.IsOverrideOrExplicitImpl = other.IsOverrideOrExplicitImpl
            && this.IsFinal = other.IsFinal
            && this.GetterOrSetterIsCompilerGenerated = other.GetterOrSetterIsCompilerGenerated
            && this.MemberKind = other.MemberKind
        | _ -> false

    override this.GetHashCode() =
        hash this.IsInstance
        + hash this.IsDispatchSlot
        + hash this.IsOverrideOrExplicitImpl
        + hash this.IsFinal
        + hash this.GetterOrSetterIsCompilerGenerated
        + hash this.MemberKind

[<StructuralEquality; NoComparison; RequireQualifiedAccess>]
type SynMemberKind =

    | ClassConstructor

    | Constructor

    | Member

    | PropertyGet

    | PropertySet

    | PropertyGetSet

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynMemberSig =

    | Member of memberSig: SynValSig * flags: SynMemberFlags * range: range * trivia: SynMemberSigMemberTrivia

    | Interface of interfaceType: SynType * range: range

    | Inherit of inheritedType: SynType * range: range

    | ValField of field: SynField * range: range

    | NestedType of nestedType: SynTypeDefnSig * range: range

    member d.Range =
        match d with
        | SynMemberSig.Member(range = m)
        | SynMemberSig.Interface(range = m)
        | SynMemberSig.Inherit(range = m)
        | SynMemberSig.ValField(range = m)
        | SynMemberSig.NestedType(range = m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnKind =
    | Unspecified
    | Class
    | Interface
    | Struct
    | Record
    | Union
    | Abbrev
    | Opaque
    | Augmentation of withKeyword: range
    | IL
    | Delegate of signature: SynType * signatureInfo: SynValInfo

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnSimpleRepr =

    | Union of accessibility: SynAccess option * unionCases: SynUnionCase list * range: range

    | Enum of cases: SynEnumCase list * range: range

    | Record of accessibility: SynAccess option * recordFields: SynField list * range: range

    | General of
        kind: SynTypeDefnKind *
        inherits: (SynType * range * Ident option) list *
        slotsigs: (SynValSig * SynMemberFlags) list *
        fields: SynField list *
        isConcrete: bool *
        isIncrClass: bool *
        implicitCtorSynPats: SynPat option *
        range: range

    | LibraryOnlyILAssembly of
        ilType: obj *  // this type is ILType but is hidden to avoid the representation of AbstractIL being public
        range: range

    | TypeAbbrev of detail: ParserDetail * rhsType: SynType * range: range

    | None of range: range

    | Exception of exnRepr: SynExceptionDefnRepr

    member this.Range =
        match this with
        | Union(range = m)
        | Enum(range = m)
        | Record(range = m)
        | General(range = m)
        | LibraryOnlyILAssembly(range = m)
        | TypeAbbrev(range = m)
        | None(range = m) -> m
        | Exception t -> t.Range

[<NoEquality; NoComparison>]
type SynEnumCase =

    | SynEnumCase of
        attributes: SynAttributes *
        ident: SynIdent *
        valueExpr: SynExpr *
        xmlDoc: PreXmlDoc *
        range: range *
        trivia: SynEnumCaseTrivia

    member this.Range =
        match this with
        | SynEnumCase(range = m) -> m

[<NoEquality; NoComparison>]
type SynUnionCase =

    | SynUnionCase of
        attributes: SynAttributes *
        ident: SynIdent *
        caseType: SynUnionCaseKind *
        xmlDoc: PreXmlDoc *
        accessibility: SynAccess option *
        range: range *
        trivia: SynUnionCaseTrivia

    member this.Range =
        match this with
        | SynUnionCase(range = m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynUnionCaseKind =

    | Fields of cases: SynField list

    | FullType of fullType: SynType * fullTypeInfo: SynValInfo

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnSigRepr =

    | ObjectModel of kind: SynTypeDefnKind * memberSigs: SynMemberSig list * range: range

    | Simple of repr: SynTypeDefnSimpleRepr * range: range

    | Exception of repr: SynExceptionDefnRepr

    member this.Range =
        match this with
        | ObjectModel(range = m)
        | Simple(range = m) -> m
        | Exception e -> e.Range

[<NoEquality; NoComparison>]
type SynTypeDefnSig =

    | SynTypeDefnSig of
        typeInfo: SynComponentInfo *
        typeRepr: SynTypeDefnSigRepr *
        members: SynMemberSig list *
        range: range *
        trivia: SynTypeDefnSigTrivia

    member this.Range =
        match this with
        | SynTypeDefnSig(range = m) -> m

[<NoEquality; NoComparison>]
type SynField =
    | SynField of
        attributes: SynAttributes *
        isStatic: bool *
        idOpt: Ident option *
        fieldType: SynType *
        isMutable: bool *
        xmlDoc: PreXmlDoc *
        accessibility: SynAccess option *
        range: range *
        trivia: SynFieldTrivia

    member this.Range =
        match this with
        | SynField(range = range) -> range

[<NoEquality; NoComparison>]
type SynComponentInfo =
    | SynComponentInfo of
        attributes: SynAttributes *
        typeParams: SynTyparDecls option *
        constraints: SynTypeConstraint list *
        longId: LongIdent *
        xmlDoc: PreXmlDoc *
        preferPostfix: bool *
        accessibility: SynAccess option *
        range: range

    member this.Range =
        match this with
        | SynComponentInfo(range = m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynValSigAccess =
    | Single of accessibility: SynAccess option
    | GetSet of accessibility: SynAccess option * getterAccessibility: SynAccess option * setterAccessibility: SynAccess option

    member this.SingleAccess() =
        match this with
        | Single(access)
        | GetSet(accessibility = access) -> access

    member this.GetSetAccessNoCheck() =
        match this with
        | Single(access) -> access, access
        | GetSet(access, getterAccess, setterAccess) ->
            let getterAccess = getterAccess |> Option.orElse access
            let setterAccess = setterAccess |> Option.orElse access
            getterAccess, setterAccess

[<NoEquality; NoComparison>]
type SynValSig =
    | SynValSig of
        attributes: SynAttributes *
        ident: SynIdent *
        explicitTypeParams: SynValTyparDecls *
        synType: SynType *
        arity: SynValInfo *
        isInline: bool *
        isMutable: bool *
        xmlDoc: PreXmlDoc *
        accessibility: SynValSigAccess *
        synExpr: SynExpr option *
        range: range *
        trivia: SynValSigTrivia

    member x.RangeOfId = let (SynValSig(ident = SynIdent(id, _))) = x in id.idRange

    member x.SynInfo = let (SynValSig(arity = v)) = x in v

    member x.SynType = let (SynValSig(synType = ty)) = x in ty

[<NoEquality; NoComparison>]
type SynValInfo =

    | SynValInfo of curriedArgInfos: SynArgInfo list list * returnInfo: SynArgInfo

    member x.CurriedArgInfos = (let (SynValInfo(args, _)) = x in args)

    member x.ArgNames =
        x.CurriedArgInfos
        |> List.concat
        |> List.map (fun info -> info.Ident)
        |> List.choose id
        |> List.map (fun id -> id.idText)

[<NoEquality; NoComparison>]
type SynArgInfo =

    | SynArgInfo of attributes: SynAttributes * optional: bool * ident: Ident option

    member x.Ident: Ident option = let (SynArgInfo(_, _, id)) = x in id

    member x.Attributes: SynAttributes = let (SynArgInfo(attrs, _, _)) = x in attrs

[<NoEquality; NoComparison>]
type SynValTyparDecls = SynValTyparDecls of typars: SynTyparDecls option * canInfer: bool

[<NoEquality; NoComparison>]
type SynReturnInfo =
    | SynReturnInfo of returnType: (SynType * SynArgInfo) * range: range

    member this.Range =
        match this with
        | SynReturnInfo(range = m) -> m

[<NoEquality; NoComparison>]
type SynExceptionDefnRepr =

    | SynExceptionDefnRepr of
        attributes: SynAttributes *
        caseName: SynUnionCase *
        longId: LongIdent option *
        xmlDoc: PreXmlDoc *
        accessibility: SynAccess option *
        range: range

    member this.Range =
        match this with
        | SynExceptionDefnRepr(range = m) -> m

[<NoEquality; NoComparison>]
type SynExceptionDefn =

    | SynExceptionDefn of exnRepr: SynExceptionDefnRepr * withKeyword: range option * members: SynMemberDefns * range: range

    member this.Range =
        match this with
        | SynExceptionDefn(range = m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynTypeDefnRepr =

    | ObjectModel of kind: SynTypeDefnKind * members: SynMemberDefns * range: range

    | Simple of simpleRepr: SynTypeDefnSimpleRepr * range: range

    | Exception of exnRepr: SynExceptionDefnRepr

    member this.Range =
        match this with
        | ObjectModel(range = m)
        | Simple(range = m) -> m
        | Exception t -> t.Range

[<NoEquality; NoComparison>]
type SynTypeDefn =
    | SynTypeDefn of
        typeInfo: SynComponentInfo *
        typeRepr: SynTypeDefnRepr *
        members: SynMemberDefns *
        implicitConstructor: SynMemberDefn option *
        range: range *
        trivia: SynTypeDefnTrivia

    member this.Range =
        match this with
        | SynTypeDefn(range = m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynMemberDefn =

    | Open of target: SynOpenDeclTarget * range: range

    | Member of memberDefn: SynBinding * range: range

    | GetSetMember of
        memberDefnForGet: SynBinding option *
        memberDefnForSet: SynBinding option *
        range: range *
        trivia: SynMemberGetSetTrivia

    | ImplicitCtor of
        accessibility: SynAccess option *
        attributes: SynAttributes *
        ctorArgs: SynPat *
        selfIdentifier: Ident option *
        xmlDoc: PreXmlDoc *
        range: range *
        trivia: SynMemberDefnImplicitCtorTrivia

    | ImplicitInherit of
        inheritType: SynType *
        inheritArgs: SynExpr *
        inheritAlias: Ident option *
        range: range *
        trivia: SynMemberDefnInheritTrivia

    | LetBindings of bindings: SynBinding list * isStatic: bool * isRecursive: bool * range: range

    | AbstractSlot of slotSig: SynValSig * flags: SynMemberFlags * range: range * trivia: SynMemberDefnAbstractSlotTrivia

    | Interface of interfaceType: SynType * withKeyword: range option * members: SynMemberDefns option * range: range

    | Inherit of baseType: SynType option * asIdent: Ident option * range: range * trivia: SynMemberDefnInheritTrivia

    | ValField of fieldInfo: SynField * range: range

    | NestedType of typeDefn: SynTypeDefn * accessibility: SynAccess option * range: range

    | AutoProperty of
        attributes: SynAttributes *
        isStatic: bool *
        ident: Ident *
        typeOpt: SynType option *
        propKind: SynMemberKind *
        memberFlags: SynMemberFlags *
        memberFlagsForSet: SynMemberFlags *
        xmlDoc: PreXmlDoc *
        accessibility: SynValSigAccess *
        synExpr: SynExpr *
        range: range *
        trivia: SynMemberDefnAutoPropertyTrivia

    member d.Range =
        match d with
        | SynMemberDefn.Member(range = m)
        | SynMemberDefn.GetSetMember(range = m)
        | SynMemberDefn.Interface(range = m)
        | SynMemberDefn.Open(range = m)
        | SynMemberDefn.LetBindings(range = m)
        | SynMemberDefn.ImplicitCtor(range = m)
        | SynMemberDefn.ImplicitInherit(range = m)
        | SynMemberDefn.AbstractSlot(range = m)
        | SynMemberDefn.Inherit(range = m)
        | SynMemberDefn.ValField(range = m)
        | SynMemberDefn.AutoProperty(range = m)
        | SynMemberDefn.NestedType(range = m) -> m

type SynMemberDefns = SynMemberDefn list

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynModuleDecl =

    | ModuleAbbrev of ident: Ident * longId: LongIdent * range: range

    | NestedModule of
        moduleInfo: SynComponentInfo *
        isRecursive: bool *
        decls: SynModuleDecl list *
        isContinuing: bool *
        range: range *
        trivia: SynModuleDeclNestedModuleTrivia

    | Let of isRecursive: bool * bindings: SynBinding list * range: range

    | Expr of expr: SynExpr * range: range

    | Types of typeDefns: SynTypeDefn list * range: range

    | Exception of exnDefn: SynExceptionDefn * range: range

    | Open of target: SynOpenDeclTarget * range: range

    | Attributes of attributes: SynAttributes * range: range

    | HashDirective of hashDirective: ParsedHashDirective * range: range

    | NamespaceFragment of fragment: SynModuleOrNamespace

    member d.Range =
        match d with
        | SynModuleDecl.ModuleAbbrev(range = m)
        | SynModuleDecl.NestedModule(range = m)
        | SynModuleDecl.Let(range = m)
        | SynModuleDecl.Expr(range = m)
        | SynModuleDecl.Types(range = m)
        | SynModuleDecl.Exception(range = m)
        | SynModuleDecl.Open(range = m)
        | SynModuleDecl.HashDirective(range = m)
        | SynModuleDecl.NamespaceFragment(SynModuleOrNamespace(range = m))
        | SynModuleDecl.Attributes(range = m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynOpenDeclTarget =

    | ModuleOrNamespace of longId: SynLongIdent * range: range

    | Type of typeName: SynType * range: range

    member this.Range =
        match this with
        | ModuleOrNamespace(range = m) -> m
        | Type(range = m) -> m

[<NoEquality; NoComparison>]
type SynExceptionSig =
    | SynExceptionSig of exnRepr: SynExceptionDefnRepr * withKeyword: range option * members: SynMemberSig list * range: range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type SynModuleSigDecl =

    | ModuleAbbrev of ident: Ident * longId: LongIdent * range: range

    | NestedModule of
        moduleInfo: SynComponentInfo *
        isRecursive: bool *
        moduleDecls: SynModuleSigDecl list *
        range: range *
        trivia: SynModuleSigDeclNestedModuleTrivia

    | Val of valSig: SynValSig * range: range

    | Types of types: SynTypeDefnSig list * range: range

    | Exception of exnSig: SynExceptionSig * range: range

    | Open of target: SynOpenDeclTarget * range: range

    | HashDirective of hashDirective: ParsedHashDirective * range: range

    | NamespaceFragment of SynModuleOrNamespaceSig

    member d.Range =
        match d with
        | SynModuleSigDecl.ModuleAbbrev(range = m)
        | SynModuleSigDecl.NestedModule(range = m)
        | SynModuleSigDecl.Val(range = m)
        | SynModuleSigDecl.Types(range = m)
        | SynModuleSigDecl.Exception(range = m)
        | SynModuleSigDecl.Open(range = m)
        | SynModuleSigDecl.NamespaceFragment(SynModuleOrNamespaceSig.SynModuleOrNamespaceSig(range = m))
        | SynModuleSigDecl.HashDirective(range = m) -> m

[<Struct; RequireQualifiedAccess>]
type SynModuleOrNamespaceKind =
    | NamedModule

    | AnonModule

    | DeclaredNamespace

    | GlobalNamespace

    member x.IsModule =
        match x with
        | NamedModule
        | AnonModule -> true
        | _ -> false

[<NoEquality; NoComparison>]
type SynModuleOrNamespace =
    | SynModuleOrNamespace of
        longId: LongIdent *
        isRecursive: bool *
        kind: SynModuleOrNamespaceKind *
        decls: SynModuleDecl list *
        xmlDoc: PreXmlDoc *
        attribs: SynAttributes *
        accessibility: SynAccess option *
        range: range *
        trivia: SynModuleOrNamespaceTrivia

    member this.Range =
        match this with
        | SynModuleOrNamespace(range = m) -> m

[<NoEquality; NoComparison>]
type SynModuleOrNamespaceSig =
    | SynModuleOrNamespaceSig of
        longId: LongIdent *
        isRecursive: bool *
        kind: SynModuleOrNamespaceKind *
        decls: SynModuleSigDecl list *
        xmlDoc: PreXmlDoc *
        attribs: SynAttributes *
        accessibility: SynAccess option *
        range: range *
        trivia: SynModuleOrNamespaceSigTrivia

    member this.Range =
        match this with
        | SynModuleOrNamespaceSig(range = m) -> m

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedHashDirectiveArgument =
    | Ident of value: Ident * range: range
    | Int32 of value: Int32 * range: range
    | LongIdent of value: SynLongIdent * range: range
    | String of value: string * stringKind: SynStringKind * range: range
    | SourceIdentifier of constant: string * value: string * range: range

    member this.Range =
        match this with
        | ParsedHashDirectiveArgument.Ident(range = m)
        | ParsedHashDirectiveArgument.Int32(range = m)
        | ParsedHashDirectiveArgument.String(range = m)
        | ParsedHashDirectiveArgument.SourceIdentifier(range = m)
        | ParsedHashDirectiveArgument.LongIdent(range = m) -> m

[<NoEquality; NoComparison>]
type ParsedHashDirective = ParsedHashDirective of ident: string * args: ParsedHashDirectiveArgument list * range: range

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedImplFileFragment =

    | AnonModule of decls: SynModuleDecl list * range: range

    | NamedModule of namedModule: SynModuleOrNamespace

    | NamespaceFragment of
        longId: LongIdent *
        isRecursive: bool *
        kind: SynModuleOrNamespaceKind *
        decls: SynModuleDecl list *
        xmlDoc: PreXmlDoc *
        attributes: SynAttributes *
        range: range *
        trivia: SynModuleOrNamespaceTrivia

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedSigFileFragment =

    | AnonModule of decls: SynModuleSigDecl list * range: range

    | NamedModule of namedModule: SynModuleOrNamespaceSig

    | NamespaceFragment of
        longId: LongIdent *
        isRecursive: bool *
        kind: SynModuleOrNamespaceKind *
        decls: SynModuleSigDecl list *
        xmlDoc: PreXmlDoc *
        attributes: SynAttributes *
        range: range *
        trivia: SynModuleOrNamespaceSigTrivia

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedScriptInteraction = Definitions of defns: SynModuleDecl list * range: range

[<NoEquality; NoComparison>]
type ParsedImplFile = ParsedImplFile of hashDirectives: ParsedHashDirective list * fragments: ParsedImplFileFragment list

[<NoEquality; NoComparison>]
type ParsedSigFile = ParsedSigFile of hashDirectives: ParsedHashDirective list * fragments: ParsedSigFileFragment list

[<NoEquality; NoComparison>]
type QualifiedNameOfFile =
    | QualifiedNameOfFile of Ident

    member x.Text = (let (QualifiedNameOfFile t) = x in t.idText)

    member x.Id = (let (QualifiedNameOfFile t) = x in t)

    member x.Range = (let (QualifiedNameOfFile t) = x in t.idRange)

[<NoEquality; NoComparison>]
type ParsedImplFileInput =
    | ParsedImplFileInput of
        fileName: string *
        isScript: bool *
        qualifiedNameOfFile: QualifiedNameOfFile *
        hashDirectives: ParsedHashDirective list *
        contents: SynModuleOrNamespace list *
        flags: (bool * bool) *
        trivia: ParsedInputTrivia *
        identifiers: Set<string>

    member x.QualifiedName =
        (let (ParsedImplFileInput(qualifiedNameOfFile = qualNameOfFile)) = x in qualNameOfFile)

    member x.HashDirectives =
        (let (ParsedImplFileInput(hashDirectives = hashDirectives)) = x in hashDirectives)

    member x.FileName = (let (ParsedImplFileInput(fileName = fileName)) = x in fileName)

    member x.Contents = (let (ParsedImplFileInput(contents = contents)) = x in contents)

    member x.IsScript = (let (ParsedImplFileInput(isScript = isScript)) = x in isScript)

    member x.IsLastCompiland =
        (let (ParsedImplFileInput(flags = (isLastCompiland, _))) = x in isLastCompiland)

    member x.IsExe = (let (ParsedImplFileInput(flags = (_, isExe))) = x in isExe)

    member x.Trivia = (let (ParsedImplFileInput(trivia = trivia)) = x in trivia)

[<NoEquality; NoComparison>]
type ParsedSigFileInput =
    | ParsedSigFileInput of
        fileName: string *
        qualifiedNameOfFile: QualifiedNameOfFile *
        hashDirectives: ParsedHashDirective list *
        contents: SynModuleOrNamespaceSig list *
        trivia: ParsedInputTrivia *
        identifiers: Set<string>

    member x.QualifiedName =
        (let (ParsedSigFileInput(qualifiedNameOfFile = qualNameOfFile)) = x in qualNameOfFile)

    member x.HashDirectives =
        (let (ParsedSigFileInput(hashDirectives = hashDirectives)) = x in hashDirectives)

    member x.FileName = (let (ParsedSigFileInput(fileName = fileName)) = x in fileName)

    member x.Contents = (let (ParsedSigFileInput(contents = contents)) = x in contents)

    member x.Trivia = (let (ParsedSigFileInput(trivia = trivia)) = x in trivia)

[<NoEquality; NoComparison; RequireQualifiedAccess>]
type ParsedInput =
    | ImplFile of ParsedImplFileInput

    | SigFile of ParsedSigFileInput

    member inp.FileName =
        match inp with
        | ParsedInput.ImplFile file -> file.FileName
        | ParsedInput.SigFile file -> file.FileName

    member inp.QualifiedName =
        match inp with
        | ParsedInput.ImplFile file -> file.QualifiedName
        | ParsedInput.SigFile file -> file.QualifiedName

    member inp.Range =
        match inp with
        | ParsedInput.ImplFile(ParsedImplFileInput(contents = SynModuleOrNamespace(range = m) :: _))
        | ParsedInput.SigFile(ParsedSigFileInput(contents = SynModuleOrNamespaceSig(range = m) :: _)) -> m
        | _ -> rangeN inp.FileName 0

    [<Experimental("This FCS API is experimental and subject to change.")>]
    member inp.Identifiers =
        match inp with
        | ParsedInput.ImplFile(ParsedImplFileInput(identifiers = identifiers))
        | ParsedInput.SigFile(ParsedSigFileInput(identifiers = identifiers)) -> identifiers
