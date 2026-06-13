namespace CommMate.Models;

public enum FramingMode
{
    /// <summary>
    /// 超时帧模式：累积字节，隔 PacketTimeout ms 无新数据后作为一包发出。
    /// 适合不连续的小包（AT 指令、问答协议等）。
    /// </summary>
    Timeout,

    /// <summary>
    /// 流模式：有数据立即读出并发出，不做缓存/拼包。
    /// 适合连续数据传输（文件传输、日志输出、高速透传等）。
    /// </summary>
    Streaming
}
