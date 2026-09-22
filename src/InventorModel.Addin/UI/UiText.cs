using System;
using System.Collections.Generic;

namespace InventorModel.Addin;

internal static class UiText
{
    private static readonly Dictionary<string, string[]> Values =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chat.Title"] = Pair("InventorModel · AI建模", "InventorModel · AI Modeling"),
            ["Chat.Header"] = Pair("AI 建模", "AI Modeling"),
            ["Chat.New"] = Pair("新对话", "New"),
            ["Chat.Image"] = Pair("图片", "Image"),
            ["Chat.Workspace"] = Pair("目录", "Folder"),
            ["Chat.History"] = Pair("历史", "History"),
            ["Chat.Settings"] = Pair("设置", "Settings"),
            ["Chat.NewTip"] = Pair("开始新对话并创建新的 AI 工作目录", "Start a new conversation and AI workspace"),
            ["Chat.ImageTip"] = Pair("选择工程图或参考图片，也可在输入框直接 Ctrl+V 粘贴", "Attach an engineering drawing or reference image; Ctrl+V also works"),
            ["Chat.WorkspaceTip"] = Pair("打开当前 AI 工作目录", "Open the current AI workspace"),
            ["Chat.HistoryTip"] = Pair("批量管理、删除和导出历史对话", "Manage, delete, and export conversation history"),
            ["Chat.SettingsTip"] = Pair("AI 服务与 Agent 配置", "AI service and Agent settings"),
            ["Chat.ContextAuto"] = Pair("上下文 自动管理", "Context Auto"),
            ["Chat.Ready"] = Pair("就绪", "Ready"),
            ["Chat.Processing"] = Pair("处理中", "Working"),
            ["Chat.Completed"] = Pair("完成", "Done"),
            ["Chat.Failed"] = Pair("失败", "Failed"),
            ["Chat.Stopped"] = Pair("已停止", "Stopped"),
            ["Chat.StopText"] = Pair("已停止。", "Stopped."),
            ["Chat.Attachment"] = Pair("图片附件", "Image attachment"),
            ["Chat.Remove"] = Pair("移除", "Remove"),
            ["Chat.InputTip"] = Pair("输入建模要求。Ctrl+V 可粘贴图片，Ctrl+Enter 发送。", "Describe the model. Ctrl+V pastes an image; Ctrl+Enter sends."),
            ["Chat.Shortcuts"] = Pair("Ctrl+V 粘贴图片 · Ctrl+Enter 发送", "Ctrl+V paste image · Ctrl+Enter send"),
            ["Chat.Stop"] = Pair("停止", "Stop"),
            ["Chat.Send"] = Pair("发送", "Send"),
            ["Chat.You"] = Pair("你", "You"),
            ["Chat.Notice.Initial"] = Pair("描述要创建的零件，也可以选择、拖入或直接 Ctrl+V 粘贴工程图。AI 会在当前工作目录内保存脚本、附件和验证视图。", "Describe the part to create, or attach, drop, or paste an engineering drawing. Scripts, attachments, and verification renders stay in the current AI workspace."),
            ["Chat.Notice.Reload"] = Pair("AI 配置已更新，后续请求将使用新的配置。", "AI settings updated. New requests will use the new configuration."),
            ["Chat.Notice.New"] = Pair("新对话已开始。可以输入建模要求，也可以 Ctrl+V 粘贴工程图。", "New conversation started. Enter a modeling request or paste an engineering drawing with Ctrl+V."),
            ["Chat.Error"] = Pair("错误", "Error"),
            ["Chat.AttachmentPrefix"] = Pair("附件：", "Attachment: "),
            ["Chat.NoToolResult"] = Pair("(无返回内容)", "(no result)"),
            ["Chat.ToolWaiting"] = Pair("等待工具返回…", "Waiting for tool result…"),
            ["Chat.ToolRunning"] = Pair("运行中", "Running"),
            ["Chat.ToolDone"] = Pair("完成", "Done"),
            ["Chat.ToolFailed"] = Pair("失败", "Failed"),
            ["Chat.ToolArgs"] = Pair("参数", "Arguments"),
            ["Chat.ToolResult"] = Pair("结果", "Result"),
            ["Chat.Tool.Validate"] = Pair("验证脚本", "Validate"),
            ["Chat.Tool.Skill"] = Pair("读取技能", "Skill reference"),
            ["Chat.Tool.Status"] = Pair("检查状态", "Status"),
            ["Chat.Tool.Build"] = Pair("生成模型", "Build model"),
            ["Chat.Tool.Modify"] = Pair("修改模型", "Modify model"),
            ["Chat.Tool.Inspect"] = Pair("检查模型", "Inspect model"),
            ["Chat.Tool.Render"] = Pair("渲染四视图", "Render views"),
            ["Chat.Tool.Save"] = Pair("保存模型", "Save model"),
            ["Chat.Tool.Generic"] = Pair("工具", "Tool"),

            ["Markdown.Select"] = Pair("选择/复制文本", "Select / copy text"),
            ["Markdown.Preview"] = Pair("返回 Markdown", "Back to Markdown"),
            ["Markdown.CopySelection"] = Pair("复制所选", "Copy selection"),
            ["Markdown.CopyAll"] = Pair("复制全文", "Copy all"),
            ["Markdown.SelectionHint"] = Pair("现在可以拖选任意文字并按 Ctrl+C 复制；Esc 返回 Markdown。", "Drag to select any text and press Ctrl+C; Esc returns to Markdown."),

            ["History.Title"] = Pair("InventorModel · 历史对话", "InventorModel · History"),
            ["History.Header"] = Pair("历史对话", "Conversation history"),
            ["History.Empty"] = Pair("暂无历史对话", "No conversation history"),
            ["History.Summary"] = Pair("共 {0} 个会话，可多选后批量导出或删除；当前会话不会被删除", "{0} sessions. Multi-select to export or delete; the current session is protected."),
            ["History.Current"] = Pair("当前", "Current"),
            ["History.SelectAll"] = Pair("全选", "Select all"),
            ["History.Clear"] = Pair("清除选择", "Clear"),
            ["History.Refresh"] = Pair("刷新", "Refresh"),
            ["History.Open"] = Pair("打开", "Open"),
            ["History.Export"] = Pair("导出", "Export"),
            ["History.Delete"] = Pair("删除", "Delete"),
            ["History.CurrentProtected"] = Pair("当前正在使用的会话不能删除。", "The current session cannot be deleted."),
            ["History.DeleteConfirm"] = Pair("确定删除选中的 {0} 个历史会话？\n\n将同时删除这些会话的附件、渲染图、脚本、输出和临时文件。", "Delete the selected {0} sessions?\n\nTheir attachments, renders, scripts, outputs, and temporary files will also be deleted."),
            ["History.Deleted"] = Pair("已删除 {0} 个会话。", "Deleted {0} sessions."),
            ["History.CurrentSkipped"] = Pair(" 当前会话已跳过。", " The current session was skipped."),
            ["History.ExportFolder"] = Pair("选择历史对话导出目录", "Choose history export folder"),
            ["History.ExportPartial"] = Pair("已导出 {0} 个会话，另有 {1} 个会话导出失败。", "Exported {0} sessions; {1} failed."),

            ["Settings.Title"] = Pair("InventorModel · AI配置", "InventorModel · AI Settings"),
            ["Settings.Header"] = Pair("AI 配置", "AI Settings"),
            ["Settings.HeaderHint"] = Pair("连接、生成、Agent、语言与服务商高级参数统一在这里配置。", "Configure connection, generation, Agent, language, and provider-specific options here."),
            ["Settings.LanguageSection"] = Pair("语言", "Language"),
            ["Settings.LanguageHint"] = Pair("界面语言与 AI 回复语言相互独立。", "UI language and AI response language are independent."),
            ["Settings.UiLanguage"] = Pair("界面语言", "UI language"),
            ["Settings.ResponseLanguage"] = Pair("AI 回复语言", "AI response language"),
            ["Settings.FollowUi"] = Pair("跟随界面", "Follow UI"),
            ["Settings.Chinese"] = Pair("简体中文", "Simplified Chinese"),
            ["Settings.English"] = Pair("English", "English"),
            ["Settings.Connection"] = Pair("连接", "Connection"),
            ["Settings.ConnectionHint"] = Pair("OpenAI Compatible 接口连接与网络容错。", "OpenAI-compatible connection and network resilience."),
            ["Settings.BaseUrl"] = Pair("接口地址", "Base URL"),
            ["Settings.ApiKey"] = Pair("API Key", "API Key"),
            ["Settings.Model"] = Pair("模型", "Model"),
            ["Settings.Timeout"] = Pair("请求超时", "Timeout"),
            ["Settings.Retry"] = Pair("重试次数", "Retries"),
            ["Settings.Generation"] = Pair("生成", "Generation"),
            ["Settings.GenerationHint"] = Pair("控制随机性、推理和输出长度。", "Control randomness, reasoning, and output length."),
            ["Settings.Temperature"] = Pair("温度", "Temperature"),
            ["Settings.Reasoning"] = Pair("推理模式", "Reasoning"),
            ["Settings.ReasoningToggle"] = Pair("启用推理 / thinking", "Enable reasoning / thinking"),
            ["Settings.MaxOutput"] = Pair("最大输出 Token", "Max output tokens"),
            ["Settings.Agent"] = Pair("Agent", "Agent"),
            ["Settings.AgentHint"] = Pair("控制长任务迭代和上下文管理。Auto 不会按固定阈值提前压缩。", "Control long-running iterations and context management. Auto never compacts at a fixed early threshold."),
            ["Settings.MaxToolCalls"] = Pair("最大调用次数", "Max tool calls"),
            ["Settings.ContextWindow"] = Pair("上下文窗口", "Context window"),
            ["Settings.Advanced"] = Pair("高级请求参数", "Advanced request parameters"),
            ["Settings.AdvancedHint"] = Pair("直接合并到 OpenAI-compatible 请求顶层，用于不同服务商的扩展参数。", "Merged into the top-level OpenAI-compatible request for provider-specific options."),
            ["Settings.FormatJson"] = Pair("格式化 JSON", "Format JSON"),
            ["Settings.ResetJson"] = Pair("重置", "Reset"),
            ["Settings.Diagnostics"] = Pair("诊断", "Diagnostics"),
            ["Settings.DiagnosticsHint"] = Pair("AI 工作文件与运行日志。非关键 COM/UI 异常会记录到日志。", "AI workspace files and runtime logs. Non-critical COM/UI failures are logged."),
            ["Settings.AiFolder"] = Pair("AI目录", "AI folder"),
            ["Settings.Logs"] = Pair("日志", "Logs"),
            ["Settings.Cancel"] = Pair("取消", "Cancel"),
            ["Settings.Save"] = Pair("保存并应用", "Save & Apply"),

            ["Ribbon.Tab"] = Pair("AI建模", "AI Modeling"),
            ["Ribbon.Panel"] = Pair("AI建模", "AI Modeling"),
            ["Ribbon.Ai"] = Pair("AI 对话", "AI Chat"),
            ["Ribbon.AiDescription"] = Pair("打开 InventorModel AI 建模助手", "Open the InventorModel AI modeling assistant"),
            ["Ribbon.AiTooltip"] = Pair("通过文本或工程图创建、检查和修改 Inventor 零件", "Create, inspect, and modify Inventor parts from text or engineering drawings"),
            ["Ribbon.Settings"] = Pair("AI 配置", "AI Settings"),
            ["Ribbon.SettingsDescription"] = Pair("配置 InventorModel AI 服务", "Configure the InventorModel AI service"),
            ["Ribbon.SettingsTooltip"] = Pair("配置接口、模型、语言和 Agent 参数", "Configure endpoint, model, language, and Agent parameters")
        };

    public static bool IsEnglish(string? language) =>
        string.Equals(
            AiSettings.NormalizeUiLanguage(language ?? string.Empty),
            AiSettings.LanguageEnglish,
            StringComparison.OrdinalIgnoreCase);

    public static string Get(string? language, string key)
    {
        if (!Values.TryGetValue(key, out string[]? pair))
            return key;

        return IsEnglish(language) ? pair[1] : pair[0];
    }

    public static string Format(
        string? language,
        string key,
        params object[] args) =>
        string.Format(Get(language, key), args ?? Array.Empty<object>());

    private static string[] Pair(string chinese, string english) =>
        new[] { chinese, english };
}
