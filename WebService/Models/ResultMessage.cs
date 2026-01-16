using WebService.Models.EnumModel;

namespace WebService.Models
{
    /// <summary>
    /// API 統一回應訊息模型，用於封裝所有 API 端點的回應格式。
    /// </summary>
    /// <remarks>
    /// 此類別提供一致的 API 回應結構，包含狀態碼、呼叫方法、訊息說明及資料內容。
    /// </remarks>
    public class ResultMessage
    {
        /// <summary>
        /// 取得或設定回應的結果代碼。
        /// </summary>
        /// <value>
        /// 代表處理結果的狀態碼字串，通常對應 <see cref="EnumResultCode"/> 的值。
        /// </value>
        public string? Code { get; set; }

        /// <summary>
        /// 取得或設定呼叫的 API 方法名稱。
        /// </summary>
        /// <value>被呼叫的 API 端點或方法名稱。</value>
        public string? Method { get; set; }

        /// <summary>
        /// 取得或設定回應的訊息說明。
        /// </summary>
        /// <value>描述處理結果的訊息文字，可用於顯示給使用者或記錄用途。</value>
        public string? Message { get; set; }

        /// <summary>
        /// 取得或設定回應的資料內容。
        /// </summary>
        /// <value>
        /// API 回傳的實際資料物件，型別依據不同 API 端點而異。
        /// 當沒有資料回傳時為 <c>null</c>。
        /// </value>
        public object? Data { get; set; }
    }
}
