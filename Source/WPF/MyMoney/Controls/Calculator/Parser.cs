﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Walkabout.Controls
{

    /// <summary>
    /// This class parses a numeric expression and evaluates it. The grammar is:
    /// 
    /// expression ::= number 
    ///         | unaryop expression
    ///         | expression binaryop expression
    ///         | expression '%'
    ///         | '(' expression ')'
    ///         
    /// number ::= any valid decimal number (e.g. 1,251.72) with optional '$' sign and commas.
    ///     
    /// unaryop ::= '-'
    ///         | '+'
    /// 
    /// binaryop ::= '+'
    ///         | '-'
    ///         | '*'
    ///         | '/'
    /// 
    /// </summary>
    internal class Parser
    {
        private double number;
        private int openParens;
        private int state;

        private struct Operation
        {
            public Token Token;
            public double Number;
        }

        private readonly List<Operation> stack = new List<Operation>();

        private enum Token
        {
            Number, Plus, Minus, Multiply, Divide, LeftParen, RightParen, Dollar, Percent, EOF,
            UnaryPlus, UnaryMinus // special tokens generated by parser.
        }

        /// <summary>
        /// Parse the expression.  See States.dgml in this project for description of this.
        /// </summary>
        /// <param name="expression"></param>
        public double Parse(string expression)
        {
            foreach (Token token in this.Tokenize(expression))
            {
                switch (this.state)
                {
                    case 0:
                        switch (token)
                        {
                            case Token.Number:
                                this.Shift(new Operation() { Token = Token.Number, Number = this.number });
                                this.state = 1;
                                break;
                            case Token.Plus:
                                this.Shift(new Operation() { Token = Token.UnaryPlus });
                                this.state = 2;
                                break;
                            case Token.Minus:
                                this.Shift(new Operation() { Token = Token.UnaryMinus });
                                this.state = 2;
                                break;
                            case Token.LeftParen:
                                this.Shift(new Operation() { Token = Token.LeftParen });
                                this.openParens++;
                                break;
                            case Token.Dollar:
                                this.state = 3;
                                break;
                            default:
                                UnexpectedToken(token, Token.Number, Token.Plus, Token.Minus, Token.LeftParen);
                                break;
                        }
                        break;
                    case 1:
                        switch (token)
                        {
                            case Token.Plus:
                                this.ReduceBinaryOperator(Token.Plus); // reduce binary operator based on precedence.
                                this.Shift(new Operation() { Token = Token.Plus });
                                this.state = 0;
                                break;
                            case Token.Minus:
                                this.ReduceBinaryOperator(Token.Minus); // reduce binary operator based on precedence.
                                this.Shift(new Operation() { Token = Token.Minus });
                                this.state = 0;
                                break;
                            case Token.Multiply:
                                this.ReduceBinaryOperator(Token.Multiply); // reduce binary operator based on precedence.
                                this.Shift(new Operation() { Token = Token.Multiply });
                                this.state = 0;
                                break;
                            case Token.Divide:
                                this.ReduceBinaryOperator(Token.Divide); // reduce binary operator based on precedence.
                                this.Shift(new Operation() { Token = Token.Divide });
                                this.state = 0;
                                break;
                            case Token.Percent:
                                this.ReducePercent();
                                this.state = 1;
                                break;
                            case Token.RightParen:
                                if (this.openParens > 0)
                                {
                                    this.CloseParens();
                                    this.state = 1;
                                    this.openParens--;
                                }
                                else
                                {
                                    goto default;
                                }
                                break;
                            case Token.EOF:
                                this.ReduceBinaryOperator(Token.EOF); // reduce binary operator based on precedence.  
                                if (this.openParens > 0)
                                {
                                    throw new Exception("Expecting close parentheses')'");
                                }
                                if (this.stack.Count > 1 || this.stack[0].Token != Token.Number)
                                {
                                    throw new Exception("Internal error, expecting stack to contain one number");
                                }
                                return this.stack[0].Number;
                            default:
                                UnexpectedToken(token, Token.Plus, Token.Minus, Token.Multiply, Token.Divide, Token.Percent, Token.RightParen);
                                break;
                        }
                        break;
                    case 2:
                        switch (token)
                        {
                            case Token.Number:
                                this.Shift(new Operation() { Token = Token.Number, Number = this.number });
                                this.ReduceUnaryOperator();
                                this.state = 1;
                                break;
                            case Token.LeftParen:
                                this.Shift(new Operation() { Token = Token.LeftParen, Number = this.number });
                                this.state = 0;
                                break;
                            case Token.Dollar:
                                this.state = 3;
                                break;
                            default:
                                UnexpectedToken(token, Token.Number, Token.LeftParen);
                                break;
                        }
                        break;
                    case 3:
                        switch (token)
                        {
                            case Token.Number:
                                this.Shift(new Operation() { Token = Token.Number, Number = this.number });
                                this.ReduceUnaryOperator();
                                this.state = 1;
                                break;
                            default:
                                UnexpectedToken(token, Token.Number);
                                break;
                        }
                        break;
                }
            }
            throw new Exception("Internal error, EOF should have returned from state 1 or thrown unexpected token errors in all other states");
        }

        private static string TokenString(Token token)
        {
            switch (token)
            {
                case Token.Number:
                    return "Number";
                case Token.Plus:
                    return "Plus (+)";
                case Token.Minus:
                    return "Minus (-)";
                case Token.Multiply:
                    return "Multiply (*)";
                case Token.Divide:
                    return "Divide (/)";
                case Token.LeftParen:
                    return "Left Parenthesis (()";
                case Token.RightParen:
                    return "Right Parenthesis ())";
                case Token.Dollar:
                    return "Dollar ($)";
                case Token.Percent:
                    return "Percent (%)";
                case Token.UnaryPlus:
                    return "Plus (+)";
                case Token.UnaryMinus:
                    return "Minus (-)";
            }
            return null;
        }

        private static void UnexpectedToken(Token found, params Token[] expected)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Unexpected token " + TokenString(found) + ", expecting ");
            bool first = true;
            foreach (Token t in expected)
            {
                if (!first)
                {
                    sb.Append(", ");
                }
                sb.Append(TokenString(t));
                first = false;
            }
            throw new Exception(sb.ToString());
        }

        private void Shift(Operation op)
        {
            this.stack.Add(op);
        }

        private Operation Pop()
        {
            Operation op = new Operation();
            if (this.stack.Count > 0)
            {
                int i = this.stack.Count - 1;
                op = this.stack[i];
                this.stack.RemoveAt(i);
            }
            return op;
        }

        private void ReduceUnaryOperator()
        {
            Operation number = this.Pop();
            Operation op = this.Pop();
            if (op.Token == Token.UnaryMinus)
            {
                number.Number = -number.Number;
            }
            this.Shift(number);
        }

        private void ReduceBinaryOperator(Token token)
        {
            int len = this.stack.Count;
            while (len >= 3)
            {
                Operation right = this.stack[len - 1];
                Operation op = this.stack[len - 2];
                Operation left = this.stack[len - 3];
                if (left.Token == Token.Number && Precidence(token) <= Precidence(op.Token))
                {
                    this.Pop();
                    this.Pop();
                    this.Pop();
                    this.Shift(ComputeBinaryOperation(left, op, right));
                    len = this.stack.Count;
                }
                else
                {
                    break;
                }
            }
        }

        private static int Precidence(Token token)
        {
            switch (token)
            {
                case Token.Plus:
                    return 1;
                case Token.Minus:
                    return 1;
                case Token.Multiply:
                    return 2;
                case Token.Divide:
                    return 2;
                case Token.LeftParen:
                    return 3;
                case Token.RightParen:
                    return 3;
                default:
                    return 0;
            }
        }

        private static Operation ComputeBinaryOperation(Operation left, Operation op, Operation right)
        {
            switch (op.Token)
            {
                case Token.Plus:
                    left.Number += right.Number;
                    break;
                case Token.Minus:
                    left.Number -= right.Number;
                    break;
                case Token.Multiply:
                    left.Number *= right.Number;
                    break;
                case Token.Divide:
                    left.Number /= right.Number;
                    break;
                default:
                    throw new Exception("Expecting binary operator");
            }
            return left;
        }

        private void CloseParens()
        {
            Operation right = this.Pop();
            Operation op = this.Pop();
            while (op.Token != Token.LeftParen)
            {
                Operation left = this.Pop();
                right = ComputeBinaryOperation(left, op, right);
                op = this.Pop();
            }
            this.Shift(right);
        }

        private void ReducePercent()
        {
            Operation number = this.Pop();
            number.Number /= 100;
            this.Shift(number);
        }

        public double Number { get { return this.number; } }


        private IEnumerable<Token> Tokenize(string expression)
        {
            var culture = CultureInfo.CurrentCulture;
            string decimalSeparator = culture.NumberFormat.NumberDecimalSeparator;
            string groupSeparator = culture.NumberFormat.NumberGroupSeparator;
            
            for (int i = 0, n = expression.Length; i < n; i++)
            {
                char c = expression[i];
                if (!char.IsWhiteSpace(c))
                {
                    switch (c)
                    {
                        case '+':
                            yield return Token.Plus;
                            break;
                        case '-':
                            yield return Token.Minus;
                            break;
                        case '*':
                            yield return Token.Multiply;
                            break;
                        case '/':
                            yield return Token.Divide;
                            break;
                        case '$':
                            yield return Token.Dollar;
                            break;
                        case '%':
                            yield return Token.Percent;
                            break;
                        case '(':
                            yield return Token.LeftParen;
                            break;
                        case ')':
                            yield return Token.RightParen;
                            break;
                        default:
                            if (char.IsDigit(c) || c.ToString() == decimalSeparator || c.ToString() == groupSeparator)
                            {
                                this.number = 0;
                                double decimalFactor = 0;
                                bool foundDecimalSeparator = false;

                                while (i < n && (char.IsDigit(c) || c.ToString() == decimalSeparator || c.ToString() == groupSeparator))
                                {
                                    if (c.ToString() == groupSeparator)
                                    {
                                        // ignore thousand/group separators.
                                    }
                                    else if (c.ToString() == decimalSeparator)
                                    {
                                        if (foundDecimalSeparator)
                                        {
                                            throw new ArgumentException(string.Format("Invalid second decimal point in expression '{0}'", expression));
                                        }
                                        foundDecimalSeparator = true;
                                        decimalFactor = 10;
                                    }
                                    else
                                    {
                                        int v = Convert.ToInt16(c) - Convert.ToInt16('0');
                                        if (decimalFactor > 0)
                                        {
                                            this.number += v / decimalFactor;
                                            decimalFactor *= 10;
                                        }
                                        else
                                        {
                                            this.number = (this.number * 10) + v;
                                        }
                                    }
                                    i++;
                                    c = i < n ? expression[i] : '\0';
                                }
                                i--;
                                yield return Token.Number;
                            }
                            else
                            {
                                throw new ArgumentException(string.Format("Unexpected char '{0}'", c));
                            }
                            break;
                    }
                }
            }
            while (true)
            {
                yield return Token.EOF;
            }
        }

    }

}
