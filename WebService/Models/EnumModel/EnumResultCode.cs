namespace WebService.Models.EnumModel
{
    /// <summary>
    /// API 回應結果代碼列舉，用於標示請求處理的狀態。
    /// </summary>
    public enum EnumResultCode
    {
        /// <summary>
        /// 請求成功處理。
        /// </summary>
        Success,

        /// <summary>
        /// 找不到指定的資源或資料。
        /// </summary>
        NoFound,

        /// <summary>
        /// 請求處理失敗（業務邏輯層面的失敗）。
        /// </summary>
        Failure,

        /// <summary>
        /// 發生錯誤（系統層面的錯誤）。
        /// </summary>
        Error,

        /// <summary>
        /// 未知的狀態或無法判斷的結果。
        /// </summary>
        Unknow
    }
}
