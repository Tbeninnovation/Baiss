-- GetConversationWithMessages
SELECT
    c.*,
    m.Id, m.ConversationId, m.SenderType, m.Content, m.SentAt
FROM Conversations c
LEFT JOIN Messages m ON c.Id = m.ConversationId
WHERE c.Id = @Id
ORDER BY m.SentAt;
