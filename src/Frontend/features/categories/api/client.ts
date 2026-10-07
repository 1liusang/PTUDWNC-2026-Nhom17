import { apiClient, type ApiRequestOptions } from "@/lib/api/client";
import type { Category, CategoryDetail, CategoryInput } from "../types";

const path = "/api/v1/categories";

export function getCategories(options?: ApiRequestOptions): Promise<Category[]> {
  return apiClient.get<Category[]>(path, options);
}

export function getCategoryBySlug(
  slug: string,
  page = 1,
  options?: ApiRequestOptions,
): Promise<CategoryDetail> {
  const query = new URLSearchParams({ page: String(page), pageSize: "12" });
  return apiClient.get<CategoryDetail>(`${path}/${encodeURIComponent(slug)}?${query}`, options);
}

export function createCategory(input: CategoryInput): Promise<Category> {
  return apiClient.post<Category>(path, { body: input });
}

export function updateCategory(id: string, input: CategoryInput): Promise<Category> {
  return apiClient.put<Category>(`${path}/${encodeURIComponent(id)}`, { body: input });
}

export function deleteCategory(id: string): Promise<void> {
  return apiClient.delete<void>(`${path}/${encodeURIComponent(id)}`);
}
