using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MySqlConnector;

namespace Gumaedaehang.Services
{
    public class DatabaseService
    {
        private static DatabaseService? _instance;
        public static DatabaseService Instance => _instance ??= new DatabaseService();

        private static readonly string ConnectionString = BuildConnectionString();

        private static string BuildConnectionString()
        {
            var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";
            return $"Server=ls-b8620b4ccbdc824c0cb2bb974b1b68d676f10035.c1wq6m02cidt.ap-northeast-2.rds.amazonaws.com;" +
                   $"Port=3306;Database=predvia;User=dbmasteruser;Password={password};" +
                   "SslMode=Required;ConnectionTimeout=10;";
        }

        // 현재 로그인된 유저의 API 키
        public static string CurrentApiKey => AuthManager.Instance.Token ?? "UNKNOWN";

        // 연결 테스트
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();
                LogWindow.AddLogStatic("✅ DB 연결 성공");
                return true;
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ DB 연결 실패: {ex.Message}");
                return false;
            }
        }

        // 상품 저장 (UPSERT) - api_key로 유저 구분
        public async Task SaveProductAsync(string storeId, string productId, string? productName, string? originalName, int price, string? imageUrl, string? productUrl, string? category,
            string? userProductName = null, int shippingCost = 0, string? bossMessage = null, int selectedTaobaoIndex = -1)
        {
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    INSERT INTO products (api_key, store_id, product_id, product_name, original_name, user_product_name, price, image_url, product_url, category, shipping_cost, boss_message, selected_taobao_index)
                    VALUES (@apiKey, @storeId, @productId, @productName, @originalName, @userProductName, @price, @imageUrl, @productUrl, @category, @shippingCost, @bossMessage, @selectedTaobaoIndex)
                    ON DUPLICATE KEY UPDATE
                        product_name = COALESCE(@productName, product_name),
                        original_name = COALESCE(@originalName, original_name),
                        user_product_name = COALESCE(@userProductName, user_product_name),
                        price = IF(@price > 0, @price, price),
                        image_url = COALESCE(@imageUrl, image_url),
                        product_url = COALESCE(@productUrl, product_url),
                        category = COALESCE(@category, category),
                        shipping_cost = IF(@shippingCost > 0, @shippingCost, shipping_cost),
                        boss_message = COALESCE(@bossMessage, boss_message),
                        selected_taobao_index = IF(@selectedTaobaoIndex >= 0, @selectedTaobaoIndex, selected_taobao_index),
                        updated_at = CURRENT_TIMESTAMP", conn);

                cmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);
                cmd.Parameters.AddWithValue("@storeId", storeId);
                cmd.Parameters.AddWithValue("@productId", productId);
                cmd.Parameters.AddWithValue("@productName", (object?)productName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@originalName", (object?)originalName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@userProductName", (object?)userProductName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@imageUrl", (object?)imageUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@productUrl", (object?)productUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@category", (object?)category ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@shippingCost", shippingCost);
                cmd.Parameters.AddWithValue("@bossMessage", (object?)bossMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@selectedTaobaoIndex", selectedTaobaoIndex);

                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상품 DB 저장 실패 [{storeId}/{productId}]: {ex.Message}");
            }
        }

        // 리뷰 일괄 저장
        public async Task SaveReviewsAsync(string storeId, string productId, List<(int rating, string? content)> reviews)
        {
            if (reviews.Count == 0) return;
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();

                using var delCmd = new MySqlCommand("DELETE FROM reviews WHERE api_key = @apiKey AND store_id = @storeId AND product_id = @productId", conn);
                delCmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);
                delCmd.Parameters.AddWithValue("@storeId", storeId);
                delCmd.Parameters.AddWithValue("@productId", productId);
                await delCmd.ExecuteNonQueryAsync();

                foreach (var (rating, content) in reviews)
                {
                    using var cmd = new MySqlCommand(@"
                        INSERT INTO reviews (api_key, store_id, product_id, rating, content)
                        VALUES (@apiKey, @storeId, @productId, @rating, @content)", conn);
                    cmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);
                    cmd.Parameters.AddWithValue("@storeId", storeId);
                    cmd.Parameters.AddWithValue("@productId", productId);
                    cmd.Parameters.AddWithValue("@rating", rating);
                    cmd.Parameters.AddWithValue("@content", (object?)content ?? DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 리뷰 DB 저장 실패 [{storeId}/{productId}]: {ex.Message}");
            }
        }

        // 타오바오 페어링 저장 (기존 페어링 삭제 후 새로 삽입)
        public async Task SaveTaobaoPairingsAsync(string storeId, string productId, List<TaobaoProductData> products)
        {
            if (products.Count == 0) return;
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();

                using var delCmd = new MySqlCommand("DELETE FROM taobao_pairings WHERE api_key = @apiKey AND store_id = @storeId AND product_id = @productId", conn);
                delCmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);
                delCmd.Parameters.AddWithValue("@storeId", storeId);
                delCmd.Parameters.AddWithValue("@productId", productId);
                await delCmd.ExecuteNonQueryAsync();

                foreach (var p in products)
                {
                    decimal.TryParse(p.Price?.Replace("CN¥", "").Replace("¥", "").Trim(), out var priceVal);
                    int.TryParse(p.Sales?.Replace("+", "").Replace("件", "").Replace(",", "").Trim(), out var salesVal);

                    using var cmd = new MySqlCommand(@"
                        INSERT INTO taobao_pairings (api_key, store_id, product_id, taobao_nid, taobao_url, taobao_price, taobao_title, taobao_image_url, taobao_sales)
                        VALUES (@apiKey, @storeId, @productId, @nid, @url, @price, @title, @imageUrl, @sales)", conn);
                    cmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);
                    cmd.Parameters.AddWithValue("@storeId", storeId);
                    cmd.Parameters.AddWithValue("@productId", productId);
                    cmd.Parameters.AddWithValue("@nid", p.Nid ?? "");
                    cmd.Parameters.AddWithValue("@url", (object?)p.ProductUrl ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@price", priceVal);
                    cmd.Parameters.AddWithValue("@title", (object?)p.Title ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@imageUrl", (object?)p.ImageUrl ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@sales", salesVal);
                    await cmd.ExecuteNonQueryAsync();
                }
                LogWindow.AddLogStatic($"✅ 타오바오 페어링 DB 저장: {storeId}/{productId} → {products.Count}개");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 타오바오 페어링 DB 저장 실패 [{storeId}/{productId}]: {ex.Message}");
            }
        }

        // 키워드 저장
        public async Task SaveKeywordsAsync(string storeId, string productId, List<string> keywords)
        {
            if (keywords.Count == 0) return;
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();
                foreach (var keyword in keywords)
                {
                    using var cmd = new MySqlCommand(@"
                        INSERT IGNORE INTO keywords (api_key, store_id, product_id, keyword)
                        VALUES (@apiKey, @storeId, @productId, @keyword)", conn);
                    cmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);
                    cmd.Parameters.AddWithValue("@storeId", storeId);
                    cmd.Parameters.AddWithValue("@productId", productId);
                    cmd.Parameters.AddWithValue("@keyword", keyword);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 DB 저장 실패 [{storeId}/{productId}]: {ex.Message}");
            }
        }

        // ========== 데이터 조회 (유저별) ==========

        // 상품 및 관련 데이터 전체 삭제
        public async Task DeleteProductAsync(string storeId, string productId)
        {
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();

                var tables = new[] { "reviews", "taobao_pairings", "keywords", "products" };
                foreach (var table in tables)
                {
                    using var cmd = new MySqlCommand(
                        $"DELETE FROM {table} WHERE api_key = @apiKey AND store_id = @storeId AND product_id = @productId", conn);
                    cmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);
                    cmd.Parameters.AddWithValue("@storeId", storeId);
                    cmd.Parameters.AddWithValue("@productId", productId);
                    await cmd.ExecuteNonQueryAsync();
                }
                LogWindow.AddLogStatic($"✅ DB 삭제 완료: {storeId}/{productId}");
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ DB 삭제 실패 [{storeId}/{productId}]: {ex.Message}");
            }
        }

        // 현재 유저의 모든 상품 조회
        public async Task<List<DbProduct>> GetProductsAsync()
        {
            var list = new List<DbProduct>();
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    SELECT store_id, product_id, product_name, original_name, price, 
                           image_url, product_url, category, created_at,
                           user_product_name, shipping_cost, boss_message, selected_taobao_index
                    FROM products WHERE api_key = @apiKey
                    ORDER BY id ASC", conn);
                cmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DbProduct
                    {
                        StoreId = reader.GetString(0),
                        ProductId = reader.GetString(1),
                        ProductName = reader.IsDBNull(2) ? null : reader.GetString(2),
                        OriginalName = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Price = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                        ImageUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                        ProductUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Category = reader.IsDBNull(7) ? null : reader.GetString(7),
                        CreatedAt = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                        UserProductName = reader.IsDBNull(9) ? null : reader.GetString(9),
                        ShippingCost = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
                        BossMessage = reader.IsDBNull(11) ? null : reader.GetString(11),
                        SelectedTaobaoIndex = reader.IsDBNull(12) ? 0 : reader.GetInt32(12)
                    });
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 상품 조회 실패: {ex.Message}");
            }
            return list;
        }

        // 특정 상품의 리뷰 조회
        public async Task<List<DbReview>> GetReviewsAsync(string storeId, string productId)
        {
            var list = new List<DbReview>();
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    SELECT rating, content FROM reviews 
                    WHERE api_key = @apiKey AND store_id = @storeId AND product_id = @productId", conn);
                cmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);
                cmd.Parameters.AddWithValue("@storeId", storeId);
                cmd.Parameters.AddWithValue("@productId", productId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DbReview
                    {
                        Rating = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        Content = reader.IsDBNull(1) ? null : reader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 리뷰 조회 실패: {ex.Message}");
            }
            return list;
        }

        // 특정 상품의 타오바오 페어링 조회
        public async Task<List<DbTaobaoPairing>> GetTaobaoPairingsAsync(string storeId, string productId)
        {
            var list = new List<DbTaobaoPairing>();
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    SELECT taobao_nid, taobao_url, taobao_price, taobao_title, taobao_image_url, taobao_sales
                    FROM taobao_pairings 
                    WHERE api_key = @apiKey AND store_id = @storeId AND product_id = @productId", conn);
                cmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);
                cmd.Parameters.AddWithValue("@storeId", storeId);
                cmd.Parameters.AddWithValue("@productId", productId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    list.Add(new DbTaobaoPairing
                    {
                        Nid = reader.IsDBNull(0) ? "" : reader.GetString(0),
                        Url = reader.IsDBNull(1) ? null : reader.GetString(1),
                        Price = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2),
                        Title = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ImageUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Sales = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                    });
                }
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 타오바오 페어링 조회 실패: {ex.Message}");
            }
            return list;
        }

        // 특정 상품의 키워드 조회
        public async Task<List<string>> GetKeywordsAsync(string storeId, string productId)
        {
            var list = new List<string>();
            try
            {
                using var conn = new MySqlConnection(ConnectionString);
                await conn.OpenAsync();
                using var cmd = new MySqlCommand(@"
                    SELECT keyword FROM keywords 
                    WHERE api_key = @apiKey AND store_id = @storeId AND product_id = @productId", conn);
                cmd.Parameters.AddWithValue("@apiKey", CurrentApiKey);
                cmd.Parameters.AddWithValue("@storeId", storeId);
                cmd.Parameters.AddWithValue("@productId", productId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    list.Add(reader.GetString(0));
            }
            catch (Exception ex)
            {
                LogWindow.AddLogStatic($"❌ 키워드 조회 실패: {ex.Message}");
            }
            return list;
        }
    }

    // ========== DB 조회용 모델 ==========

    public class DbProduct
    {
        public string StoreId { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string? ProductName { get; set; }
        public string? OriginalName { get; set; }
        public string? UserProductName { get; set; }
        public int Price { get; set; }
        public string? ImageUrl { get; set; }
        public string? ProductUrl { get; set; }
        public string? Category { get; set; }
        public int ShippingCost { get; set; }
        public string? BossMessage { get; set; }
        public int SelectedTaobaoIndex { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DbReview
    {
        public int Rating { get; set; }
        public string? Content { get; set; }
    }

    public class DbTaobaoPairing
    {
        public string Nid { get; set; } = "";
        public string? Url { get; set; }
        public decimal Price { get; set; }
        public string? Title { get; set; }
        public string? ImageUrl { get; set; }
        public int Sales { get; set; }
    }
}
