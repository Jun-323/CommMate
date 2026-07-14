# CommMate DSL Script Syntax Reference

---

## Sending

| Syntax | Description |
|------|------|
| `SEND <text>` | Send text, automatically appends CRLF newline |
| `SENDN <text>` | Send text without appending newline |
| `HEX <hex bytes>` | Send hexadecimal data |

```cmds
SEND AT
SEND AT+CSQ
SENDN Hello
HEX 41 54 0D 0A
```

---

## Delay / Waiting for Response

| Syntax | Description |
|------|------|
| `WAIT <ms>` | Pure delay, does nothing for N milliseconds |
| `EXPECT "<regex>" IN <ms>` | Wait for module response matching regex, up to N ms (`IN` can be omitted, defaults to 3000ms) |
| `EXPECT "<regex>" IN <ms> -> var` | Same as above, writes first capture group to variable on match |

**WAIT vs EXPECT:**

| | WAIT | EXPECT |
|---|---|---|
| Behavior | Wait N ms unconditionally | Wait for match, continue immediately on success |
| Timeout | No timeout concept | Timeout triggers `IF TIMEOUT` branch |
| Use case | Module needs processing time but no response | Most AT commands |

```cmds
# Typical usage
SEND AT+CIPSEND=10
EXPECT ">" IN 5000         # Wait for > prompt, up to 5s
SENDN Hello                 # Send data
WAIT 2000                   # Wait for network transmission
SEND AT+CIPCLOSE
EXPECT "OK" IN 5000
```

**Expect matching rules:**
- Regular expressions, C# syntax (`Regex.IsMatch`), case-insensitive
- Accumulates from first byte received after the `EXPECT` instruction
- Timeout is non-fatal, `IF TIMEOUT` branch takes effect, execution continues

---

## Variables

| Syntax | Description |
|------|------|
| `SET name = value` | Assignment (supports `$var` references and `+ - * /` operations) |
| `$varName` | Variable reference, usable in `SEND`, `LOG`, `ASSERT` |

```cmds
SET retry = 0
SET retry = $retry + 1
SEND AT+CREG=$n
LOG "Signal: $csq"
```

- Variable scope: script-global, available everywhere after `SET`
- Values are strings, attempted integer conversion during arithmetic

---

## Conditional Jumps

| Syntax | Description |
|------|------|
| `IF OK GOTO label` | Last `EXPECT` matched → jump |
| `IF TIMEOUT GOTO label` | Last `EXPECT` timed out → jump |
| `IF $var == value GOTO label` | Variable condition jump |
| `GOTO label` | Unconditional jump |

Supported comparison operators: `==` `!=` `<` `>` `<=` `>=`

```cmds
SEND AT
EXPECT "OK" IN 3000
IF TIMEOUT GOTO fail        # Timeout → failure handler

IF $retry < 5 GOTO check    # Under 5 retries, try again
GOTO done                   # Skip failure branch

:fail
LOG "Failed"

:done
LOG "Done"
```

---

## Loops

| Syntax | Description |
|------|------|
| `REPEAT <N>` ... `END` | Repeat N times |
| `WHILE $var <op> value` ... `END` | Conditional loop |

```cmds
REPEAT 10
    SEND AT+CSQ
    EXPECT "OK" IN 2000
    WAIT 2000
END

SET retry = 0
:check
SEND AT+CREG?
EXPECT "OK" IN 3000
SET retry = $retry + 1
IF $retry < 5 GOTO check
```

---

## Labels

```
:labelname   Define a jump target
```

Label names consist of letters, digits, and underscores.

```cmds
IF OK GOTO done
GOTO retry
:retry
:done
```

---

## Log / Assert / Stop

| Syntax | Description |
|------|------|
| `LOG "text $var"` | Output log to script panel Output area, supports `$var` interpolation |
| `ASSERT "expr"` | Assertion failure → stop script and report error |
| `STOP` | Immediately stop script |

```cmds
LOG "=== Test Start ==="
LOG "Signal: $csq"
ASSERT "$retry < 10"
STOP
```

---

## Comments

```
# This is a comment   Line comment (# to end of line)
```

---

## Full Examples

### EC801E Basic AT Test

```cmds
# ============================
# EC801E Basic AT Test
# ============================

LOG "=== Start ==="

SEND AT
EXPECT "OK" IN 3000
IF TIMEOUT GOTO fail

SEND AT+CGMI
EXPECT "OK" IN 3000

SEND AT+CGSN
EXPECT "\d{15}" IN 5000 -> imei
LOG "IMEI: $imei"

SEND AT+CSQ
EXPECT "\+CSQ:\s*(\d+)" IN 3000 -> csq
LOG "Signal: $csq"

# Loop waiting for network registration
SET retry = 0
:wait_reg
SEND AT+CREG?
EXPECT "OK" IN 3000
SET retry = $retry + 1
IF $retry < 10 GOTO wait_reg

GOTO done

:fail
LOG "!!! Failed !!!"

:done
LOG "=== Done ==="
```

### EC801E TCP Client

```cmds
# ============================
# EC801E TCP Client Test
# ============================

LOG "=== TCP Client Test ==="

SEND AT
EXPECT "OK" IN 3000
IF TIMEOUT GOTO fail

SEND AT+CSQ
EXPECT "\+CSQ:\s*(\d+)" IN 3000 -> csq
LOG "Signal: $csq"

SEND AT+QICSGP=1,1,"CMNET","","",0
EXPECT "OK" IN 3000

SEND AT+QIACT=1
EXPECT "OK" IN 5000

SEND AT+QIOPEN=1,0,"TCP","120.79.xxx.xxx",8888,0,0
EXPECT "\+QIOPEN:\s*1,0" IN 15000
IF TIMEOUT GOTO fail

LOG "TCP Connected"

SEND AT+QISEND=1,12
EXPECT ">" IN 5000
SENDN Hello World!

WAIT 3000

SEND AT+QICLOSE=1
EXPECT "OK" IN 5000

GOTO done

:fail
LOG "!!! Failed !!!"

:done
SEND AT+QIDEACT=1
EXPECT "OK" IN 5000
LOG "=== Done ==="
```

---

## Error Handling

| Scenario | Behavior |
|------|------|
| `EXPECT` timeout | Continue to next step, `IF TIMEOUT` branch takes effect |
| `ASSERT` failure | Immediately stop, raise error |
| `GOTO` undefined label | Parse error, refuse to run |
| Syntax error | Parse error, refuse to run |
| Serial port not open | Prompt before running |
