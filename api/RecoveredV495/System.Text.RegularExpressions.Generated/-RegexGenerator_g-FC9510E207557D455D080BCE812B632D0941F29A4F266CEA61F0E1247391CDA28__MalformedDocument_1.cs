using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.32403")]
internal sealed class _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__MalformedDocument_1 : Regex
{
	private sealed class RunnerFactory : RegexRunnerFactory
	{
		private sealed class Runner : RegexRunner
		{
			protected override void Scan(ReadOnlySpan<char> inputSpan)
			{
				while (TryFindNextPossibleStartingPosition(inputSpan) && !TryMatchAtCurrentPosition(inputSpan) && runtextpos != inputSpan.Length)
				{
					runtextpos++;
					if (_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
				}
			}

			private bool TryFindNextPossibleStartingPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				if (num <= inputSpan.Length - 22)
				{
					int num2 = inputSpan.Slice(num).IndexOfNonAsciiOrAny_4B95C560E5DFB44DD6E428EA77A42135468693390868E1935BB41CAA776BAAB6();
					if (num2 >= 0)
					{
						runtextpos = num + num2;
						return true;
					}
				}
				runtextpos = inputSpan.Length;
				return false;
			}

			private bool TryMatchAtCurrentPosition(ReadOnlySpan<char> inputSpan)
			{
				int num = runtextpos;
				int start = num;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				int num5 = 0;
				int num6 = 0;
				int num7 = 0;
				int num8 = 0;
				int num9 = 0;
				int num10 = 0;
				int num11 = 0;
				int num12 = 0;
				int num13 = 0;
				int num14 = 0;
				ReadOnlySpan<char> span = inputSpan.Slice(num);
				num4 = num;
				num3 = Crawlpos();
				if (num != 0)
				{
					goto IL_0049;
				}
				num2 = 0;
				goto IL_00ae;
				IL_0049:
				num = num4;
				span = inputSpan.Slice(num);
				UncaptureUntil(num3);
				if (span.IsEmpty || _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.IsWordChar(span[0]))
				{
					UncaptureUntil(0);
					return false;
				}
				num2 = 1;
				num++;
				span = inputSpan.Slice(num);
				goto IL_00ae;
				IL_00ae:
				while (true)
				{
					num5 = num;
					if (!span.IsEmpty)
					{
						char c = span[0];
						if (c != 'A')
						{
							if (c != 'T' || !span.Slice(1).StartsWith("AL"))
							{
								goto IL_0091;
							}
							num += 3;
							span = inputSpan.Slice(num);
						}
						else
						{
							if (!span.Slice(1).StartsWith("LM"))
							{
								goto IL_0091;
							}
							num += 3;
							span = inputSpan.Slice(num);
						}
						Capture(1, num5, num);
						num11 = num;
						int i;
						for (i = 0; (uint)i < (uint)span.Length && !_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.IsWordChar(span[i]); i++)
						{
						}
						span = span.Slice(i);
						num += i;
						num12 = num;
						while (true)
						{
							num9 = Crawlpos();
							num6 = num;
							if ((uint)span.Length >= 8u && char.IsDigit(span[0]) && char.IsDigit(span[1]) && char.IsDigit(span[2]) && char.IsDigit(span[3]) && char.IsDigit(span[4]) && char.IsDigit(span[5]) && char.IsDigit(span[6]) && char.IsDigit(span[7]))
							{
								num += 8;
								span = inputSpan.Slice(num);
								Capture(2, num6, num);
								num13 = num;
								int j;
								for (j = 0; (uint)j < (uint)span.Length && !_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.IsWordChar(span[j]); j++)
								{
								}
								span = span.Slice(j);
								num += j;
								num14 = num;
								while (true)
								{
									num10 = Crawlpos();
									num7 = num;
									if ((uint)span.Length >= 6u && char.IsDigit(span[0]) && char.IsDigit(span[1]) && char.IsDigit(span[2]) && char.IsDigit(span[3]) && char.IsDigit(span[4]) && char.IsDigit(span[5]))
									{
										num += 6;
										span = inputSpan.Slice(num);
										Capture(3, num7, num);
										int k;
										for (k = 0; (uint)k < (uint)span.Length && !_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.IsWordChar(span[k]); k++)
										{
										}
										span = span.Slice(k);
										num += k;
										num8 = num;
										int num15 = span.IndexOfAnyExcept(_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_ascii_FF03FEFFFF0700000000);
										if (num15 < 0)
										{
											num15 = span.Length;
										}
										if (num15 >= 5)
										{
											span = span.Slice(num15);
											num += num15;
											Capture(4, num8, num);
											if (num >= inputSpan.Length - 1 && ((uint)num >= (uint)inputSpan.Length || inputSpan[num] == '\n'))
											{
												runtextpos = num;
												Capture(0, start, num);
												return true;
											}
										}
									}
									UncaptureUntil(num10);
									if (_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_hasTimeout)
									{
										CheckTimeout();
									}
									if (num13 >= num14)
									{
										break;
									}
									num = --num14;
									span = inputSpan.Slice(num);
								}
							}
							UncaptureUntil(num9);
							if (_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_hasTimeout)
							{
								CheckTimeout();
							}
							if (num11 >= num12)
							{
								break;
							}
							num = --num12;
							span = inputSpan.Slice(num);
						}
					}
					goto IL_0091;
					IL_0091:
					if (_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
					if (num2 == 0)
					{
						break;
					}
					if (num2 == 1)
					{
						UncaptureUntil(0);
						return false;
					}
				}
				goto IL_0049;
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				void UncaptureUntil(int capturePosition)
				{
					while (Crawlpos() > capturePosition)
					{
						Uncapture();
					}
				}
			}
		}

		protected override RegexRunner CreateInstance()
		{
			return new Runner();
		}
	}

	internal static readonly _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__MalformedDocument_1 Instance = new _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__MalformedDocument_1();

	private _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__MalformedDocument_1()
	{
		pattern = "(?:^|\\W)(ALM|TAL)\\W*(\\d{8})\\W*(\\d{6})\\W*([A-Z0-9]{5,})$";
		roptions = RegexOptions.None;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 5;
	}
}
