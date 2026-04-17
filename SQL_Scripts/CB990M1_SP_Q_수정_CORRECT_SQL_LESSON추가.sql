-- ═══════════════════════════════════════════════════════════════════════════
-- USP_CB990M1_CKO087_Q 수정 — CORRECT_SQL, LESSON 컬럼 추가 반환
-- PA999_FEEDBACK_PATTERN LEFT JOIN으로 기존 패턴 데이터 함께 조회
-- ═══════════════════════════════════════════════════════════════════════════
ALTER PROCEDURE dbo.USP_CB990M1_CKO087_Q
    @USER_ID    NVARCHAR(60)  = NULL,
    @FROM_DT    NVARCHAR(10)  = NULL,
    @TO_DT      NVARCHAR(10)  = NULL,
    @IS_ERROR   NVARCHAR(1)   = NULL,
    @SEARCH_KWD NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @v_USER_ID     NVARCHAR(60)  = NULLIF(LTRIM(RTRIM(@USER_ID)), '')
    DECLARE @v_SEARCH_KWD  NVARCHAR(200) = NULLIF(LTRIM(RTRIM(@SEARCH_KWD)), '')

    DECLARE @v_FROM_DT DATETIME = NULL
    DECLARE @v_TO_DT   DATETIME = NULL
    IF NULLIF(@FROM_DT, '') IS NOT NULL
        SET @v_FROM_DT = CONVERT(DATETIME, LEFT(@FROM_DT, 10), 120)
    IF NULLIF(@TO_DT, '') IS NOT NULL
        SET @v_TO_DT   = DATEADD(DAY, 1, CONVERT(DATETIME, LEFT(@TO_DT, 10), 120))

    DECLARE @v_IS_ERR TINYINT = NULL
    IF NULLIF(@IS_ERROR, '') IS NOT NULL
        SET @v_IS_ERR = CASE @IS_ERROR WHEN 'T' THEN 1 ELSE 0 END

    SELECT
        L.LOG_SEQ,
        L.SESSION_ID,
        L.USER_ID,
        L.USER_QUERY,
        L.AI_RESPONSE,
        L.GENERATED_SQL,
        L.RELATED_TABLES,
        CONVERT(BIT, L.IS_ERROR)                  AS IS_ERROR,
        CONVERT(NVARCHAR(19), L.CREATED_DT, 120)  AS CREATED_DT,
        L.PERF_SCORE,
        CASE
            WHEN L.PERF_SCORE = 5       THEN 'GOLD'
            WHEN L.PERF_SCORE = 4       THEN 'PASS'
            WHEN L.PERF_SCORE = 3       THEN 'REVIEW'
            WHEN L.PERF_SCORE IN (1, 2) THEN 'FAIL'
            ELSE ''
        END                                       AS SCORE_LABEL,
        L.DEV_FEEDBACK,
        CONVERT(NVARCHAR(19), L.FEEDBACK_DT, 120) AS FEEDBACK_DT,
        L.FEEDBACK_BY,
        -- ★ 신규: FEEDBACK_PATTERN에서 올바른SQL + 교훈 가져오기
        FP.CORRECT_SQL,
        FP.LESSON
    FROM dbo.PA999_CHAT_LOG L (NOLOCK)
    -- 동일 LOG_SEQ에 여러 패턴이 있을 수 있으므로 최신 1건만
    OUTER APPLY (
        SELECT TOP 1 P.CORRECT_SQL, P.LESSON
        FROM dbo.PA999_FEEDBACK_PATTERN P (NOLOCK)
        WHERE P.LOG_SEQ = L.LOG_SEQ
          AND P.APPLY_YN = 'Y'
        ORDER BY P.PATTERN_SEQ DESC
    ) FP
    WHERE (@v_FROM_DT IS NULL OR L.CREATED_DT >= @v_FROM_DT)
      AND (@v_TO_DT   IS NULL OR L.CREATED_DT <  @v_TO_DT)
      AND (@v_USER_ID IS NULL OR L.USER_ID     =  @v_USER_ID)
      AND (@v_IS_ERR  IS NULL OR L.IS_ERROR    =  @v_IS_ERR)
      AND (@v_SEARCH_KWD IS NULL
           OR L.USER_QUERY LIKE '%' + @v_SEARCH_KWD + '%')
    ORDER BY L.LOG_SEQ DESC
    OPTION (RECOMPILE)
END
