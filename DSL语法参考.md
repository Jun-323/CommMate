# CommMate DSL 脚本语法参考

---

## 发送

| 语法 | 说明 |
|------|------|
| `SEND <text>` | 发送文本，自动追加 CRLF 换行 |
| `SENDN <text>` | 发送文本，不追加换行 |
| `HEX <hex bytes>` | 发送十六进制数据 |

```cmds
SEND AT
SEND AT+CSQ
SENDN Hello
HEX 41 54 0D 0A
```

---

## 延时 / 等待响应

| 语法 | 说明 |
|------|------|
| `WAIT <ms>` | 纯延时，什么都不做，等待 N 毫秒 |
| `EXPECT "<regex>" IN <ms>` | 等待模块返回匹配正则的响应，最长等 N 毫秒（`IN` 可省略，省略时默认 3000ms） |
| `EXPECT "<regex>" IN <ms> -> var` | 同上，匹配成功后将第一个捕获组写入变量 |

**WAIT vs EXPECT：**

| | WAIT | EXPECT |
|---|---|---|
| 行为 | 死等 N 毫秒 | 等匹配，匹配成功立刻继续 |
| 超时 | 不存在超时概念 | 超时后走 `IF TIMEOUT` 分支 |
| 适用场景 | 模组需要处理时间但不回数据 | 绝大多数 AT 指令 |

```cmds
# 典型配合用法
SEND AT+CIPSEND=10
EXPECT ">" IN 5000         # 等 > 提示，最长 5s
SENDN Hello                 # 发数据
WAIT 2000                   # 等网络传输
SEND AT+CIPCLOSE
EXPECT "OK" IN 5000
```

**Expect 匹配规则：**
- 正则表达式，C# 语法 (`Regex.IsMatch`)，大小写不敏感
- 从 `EXPECT` 指令后收到的第一个字节开始累积匹配
- 超时非致命，`IF TIMEOUT` 分支生效，继续执行下一步

---

## 变量

| 语法 | 说明 |
|------|------|
| `SET name = value` | 赋值（支持 `$var` 引用和 `+ - * /` 运算） |
| `$变量名` | 引用变量，可在 `SEND`、`LOG`、`ASSERT` 中使用 |

```cmds
SET retry = 0
SET retry = $retry + 1
SEND AT+CREG=$n
LOG "信号值: $csq"
```

- 变量作用域：脚本全局，`SET` 后全程可用
- 值都是字符串，运算时尝试转为整数

---

## 条件跳转

| 语法 | 说明 |
|------|------|
| `IF OK GOTO label` | 上条 `EXPECT` 匹配成功 → 跳转 |
| `IF TIMEOUT GOTO label` | 上条 `EXPECT` 超时 → 跳转 |
| `IF $var == value GOTO label` | 变量条件跳转 |
| `GOTO label` | 无条件跳转 |

支持的比较符：`==` `!=` `<` `>` `<=` `>=`

```cmds
SEND AT
EXPECT "OK" IN 3000
IF TIMEOUT GOTO fail        # 超时跳去失败处理

IF $retry < 5 GOTO check    # 还没到 5 次，继续重试
GOTO done                   # 跳过失败分支

:fail
LOG "失败"

:done
LOG "完成"
```

---

## 循环

| 语法 | 说明 |
|------|------|
| `REPEAT <N>` ... `END` | 重复 N 次 |
| `WHILE $var <op> value` ... `END` | 条件循环 |

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

## 标签

```
:labelname   定义跳转目标
```

标签名由英文、数字、下划线组成。

```cmds
IF OK GOTO done
GOTO retry
:retry
:done
```

---

## 日志 / 断言 / 停止

| 语法 | 说明 |
|------|------|
| `LOG "text $var"` | 输出日志到脚本面板的 Output 区域，支持 `$var` 插值 |
| `ASSERT "expr"` | 断言失败 → 停止脚本并报错 |
| `STOP` | 立即停止脚本 |

```cmds
LOG "=== 测试开始 ==="
LOG "信号值: $csq"
ASSERT "$retry < 10"
STOP
```

---

## 注释

```
# 这是注释   行注释（# 到行尾）
```

---

## 完整示例

### EC801E 基础 AT 测试

```cmds
# ============================
# EC801E 基础 AT 测试
# ============================

LOG "=== 开始 ==="

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
LOG "信号值: $csq"

# 循环等待网络注册
SET retry = 0
:wait_reg
SEND AT+CREG?
EXPECT "OK" IN 3000
SET retry = $retry + 1
IF $retry < 10 GOTO wait_reg

GOTO done

:fail
LOG "!!! 失败 !!!"

:done
LOG "=== 完成 ==="
```

### EC801E TCP Client

```cmds
# ============================
# EC801E TCP Client 连接测试
# ============================

LOG "=== TCP Client 测试 ==="

SEND AT
EXPECT "OK" IN 3000
IF TIMEOUT GOTO fail

SEND AT+CSQ
EXPECT "\+CSQ:\s*(\d+)" IN 3000 -> csq
LOG "信号值: $csq"

SEND AT+QICSGP=1,1,"CMNET","","",0
EXPECT "OK" IN 3000

SEND AT+QIACT=1
EXPECT "OK" IN 5000

SEND AT+QIOPEN=1,0,"TCP","120.79.xxx.xxx",8888,0,0
EXPECT "\+QIOPEN:\s*1,0" IN 15000
IF TIMEOUT GOTO fail

LOG "TCP 连接成功"

SEND AT+QISEND=1,12
EXPECT ">" IN 5000
SENDN Hello World!

WAIT 3000

SEND AT+QICLOSE=1
EXPECT "OK" IN 5000

GOTO done

:fail
LOG "!!! 失败 !!!"

:done
SEND AT+QIDEACT=1
EXPECT "OK" IN 5000
LOG "=== 完成 ==="
```

---

## 错误处理

| 情况 | 行为 |
|------|------|
| `EXPECT` 超时 | 继续下一步，`IF TIMEOUT` 分支生效 |
| `ASSERT` 失败 | 立即停止，弹错误 |
| `GOTO` 未定义标签 | 解析时报错，拒绝运行 |
| 语法错误 | 解析时报错，拒绝运行 |
| 串口未打开 | 运行前提示 |
