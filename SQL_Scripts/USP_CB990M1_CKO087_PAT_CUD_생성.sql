-- ═══════════════════════════════════════════════════════════════════════════
-- USP_CB990M1_CKO087_PAT_CUD — AI 피드백 패턴 등록 SP
-- CB990M1 화면의 "AI 패턴 등록" 버튼에서 호출
-- PA999_FEEDBACK_PATTERN 테이블에 INSERT
-- ═══════════════════════════════════════════════════════════════════════════
IF OBJECT_ID('dbo.USP_CB990M1_CKO087_PAT_CUD', 'P') IS NOT NULL
    DROP PROCEDURE dbo.USP_CB990M1_CKO087_PAT_CUD;
GO

CREATE PROCEDURE dbo.USP_CB990M1_CKO087_PAT_CUD
    @P_LOG_SEQ         BIGINT          = NULL,
    @P_LESSON          NVARCHAR(2000)  = NULL,
    @P_WRONG_APPROACH  NVARCHAR(MAX)   = NULL,
    @P_CORRECT_SQL     NVARCHAR(MAX)   = NULL,
    @P_PRIORITY        TINYINT         = 5,
    @P_USER_ID         NVARCHAR(60)    = NULL,
    @P_MSG_CD          NVARCHAR(6)     = '' OUTPUT,
    @P_MESSAGE         NVARCHAR(200)   = '' OUTPUT
AS
BEGIN
    SET NOCOUNT ON
    SET XACT_ABORT ON

    BEGIN TRY
        BEGIN TRAN

        INSERT INTO dbo.PA999_FEEDBACK_PATTERN
            (LOG_SEQ, QUERY_PATTERN, WRONG_APPROACH, CORRECT_SQL,
             LESSON, PRIORITY, APPLY_YN,
             INSRT_USER_ID, INSRT_DT, UPDT_USER_ID, UPDT_DT)
        VALUES
            (@P_LOG_SEQ,
             ISNULL(@P_LESSON, ''),
             NULLIF(@P_WRONG_APPROACH, ''),
             NULLIF(@P_CORRECT_SQL, ''),
             ISNULL(@P_LESSON, ''),
             @P_PRIORITY,
             'Y',
             ISNULL(@P_USER_ID, 'SYSTEM'),
             GETDATE(),
             ISNULL(@P_USER_ID, 'SYSTEM'),
             GETDATE())

        COMMIT TRAN
        SET @P_MSG_CD  = '900001'
        SET @P_MESSAGE = 'AI 패턴이 등록되었습니다.'
        RETURN 0

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN
        SET @P_MSG_CD  = 'ER9999'
        SET @P_MESSAGE = ERROR_MESSAGE()
        RETURN -1
    END CATCH
END
GO
