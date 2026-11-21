using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Generated;

[GeneratedCode("System.Text.RegularExpressions.Generator", "8.0.14.32403")]
internal sealed class _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__MalformedPrefixedQr_0 : Regex
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
				if (num <= inputSpan.Length - 29)
				{
					int num2 = inputSpan.Slice(num).IndexOf("REAL");
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
				ReadOnlySpan<char> span = inputSpan.Slice(num);
				if (!span.StartsWith("REAL"))
				{
					UncaptureUntil(0);
					return false;
				}
				int i;
				for (i = 4; (uint)i < (uint)span.Length && !_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.IsWordChar(span[i]); i++)
				{
				}
				span = span.Slice(i);
				num += i;
				if (!span.StartsWith("MNT"))
				{
					UncaptureUntil(0);
					return false;
				}
				int j;
				for (j = 3; (uint)j < (uint)span.Length && !_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.IsWordChar(span[j]); j++)
				{
				}
				span = span.Slice(j);
				num += j;
				num2 = num;
				if (span.IsEmpty)
				{
					UncaptureUntil(0);
					return false;
				}
				switch (span[0])
				{
				case 'A':
					if (!span.Slice(1).StartsWith("LM"))
					{
						UncaptureUntil(0);
						return false;
					}
					num += 3;
					span = inputSpan.Slice(num);
					break;
				case 'T':
					if (!span.Slice(1).StartsWith("AL"))
					{
						UncaptureUntil(0);
						return false;
					}
					num += 3;
					span = inputSpan.Slice(num);
					break;
				default:
					UncaptureUntil(0);
					return false;
				}
				Capture(1, num2, num);
				num8 = num;
				int k;
				for (k = 0; (uint)k < (uint)span.Length && !_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.IsWordChar(span[k]); k++)
				{
				}
				span = span.Slice(k);
				num += k;
				num9 = num;
				while (true)
				{
					num6 = Crawlpos();
					num3 = num;
					if ((uint)span.Length >= 8u && char.IsDigit(span[0]) && char.IsDigit(span[1]) && char.IsDigit(span[2]) && char.IsDigit(span[3]) && char.IsDigit(span[4]) && char.IsDigit(span[5]) && char.IsDigit(span[6]) && char.IsDigit(span[7]))
					{
						num += 8;
						span = inputSpan.Slice(num);
						Capture(2, num3, num);
						num10 = num;
						int l;
						for (l = 0; (uint)l < (uint)span.Length && !_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.IsWordChar(span[l]); l++)
						{
						}
						span = span.Slice(l);
						num += l;
						num11 = num;
						while (true)
						{
							num7 = Crawlpos();
							num4 = num;
							if ((uint)span.Length >= 6u && char.IsDigit(span[0]) && char.IsDigit(span[1]) && char.IsDigit(span[2]) && char.IsDigit(span[3]) && char.IsDigit(span[4]) && char.IsDigit(span[5]))
							{
								num += 6;
								span = inputSpan.Slice(num);
								Capture(3, num4, num);
								int m;
								for (m = 0; (uint)m < (uint)span.Length && !_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.IsWordChar(span[m]); m++)
								{
								}
								span = span.Slice(m);
								num += m;
								num5 = num;
								int num12 = span.IndexOfAnyExcept(_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_ascii_FF03FEFFFF0700000000);
								if (num12 < 0)
								{
									num12 = span.Length;
								}
								if (num12 >= 5)
								{
									span = span.Slice(num12);
									num += num12;
									Capture(4, num5, num);
									if (num >= inputSpan.Length - 1 && ((uint)num >= (uint)inputSpan.Length || inputSpan[num] == '\n'))
									{
										runtextpos = num;
										Capture(0, start, num);
										return true;
									}
								}
							}
							UncaptureUntil(num7);
							if (_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_hasTimeout)
							{
								CheckTimeout();
							}
							if (num10 >= num11)
							{
								break;
							}
							num = --num11;
							span = inputSpan.Slice(num);
						}
					}
					UncaptureUntil(num6);
					if (_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_hasTimeout)
					{
						CheckTimeout();
					}
					if (num8 >= num9)
					{
						break;
					}
					num = --num9;
					span = inputSpan.Slice(num);
				}
				UncaptureUntil(0);
				return false;
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

	internal static readonly _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__MalformedPrefixedQr_0 Instance = new _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__MalformedPrefixedQr_0();

	private _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__MalformedPrefixedQr_0()
	{
		pattern = "REAL\\W*MNT\\W*(ALM|TAL)\\W*(\\d{8})\\W*(\\d{6})\\W*([A-Z0-9]{5,})$";
		roptions = RegexOptions.None;
		Regex.ValidateMatchTimeout(_003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_defaultTimeout);
		internalMatchTimeout = _003CRegexGenerator_g_003EFC9510E207557D455D080BCE812B632D0941F29A4F266CEA61F0E1247391CDA28__Utilities.s_defaultTimeout;
		factory = new RunnerFactory();
		capsize = 5;
	}
}
