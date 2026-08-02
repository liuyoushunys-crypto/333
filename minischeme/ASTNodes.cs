namespace Miniscm.Compiler;

using Miniscm.Types;

abstract record AstNode;

sealed record LiteralAst(object? Val) : AstNode;
sealed record VarAst(string Name) : AstNode;
sealed record IfAst(AstNode Test, AstNode Then, AstNode Else) : AstNode;
sealed record DefineAst(string Name, AstNode Val) : AstNode;
sealed record SetBangAst(string Name, AstNode Val) : AstNode;
sealed record LambdaAst(List<string> Params, List<AstNode> Body, bool IsSimple, object? RawBody = null) : AstNode;
sealed record BeginAst(List<AstNode> Exprs) : AstNode;
sealed record AppAst(AstNode Proc, List<AstNode> Args) : AstNode;
